using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using AngleSharp.Html.Dom;
using MyYoutubeNow.Utils;
using NLog;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Options.Filters;
using IPlaylistProgress = System.Collections.Generic.IDictionary<YoutubeExplode.Videos.VideoId, MyYoutubeNow.Progress.IVideoProgress>;

namespace MyYoutubeNow.Client
{
    public class Chapter
    {
        public string Title { get; }
        public ulong TimeRangeStartMs { get; }
        public Chapter(string title, ulong timeRangeStart)
        {
            Title = title;
            TimeRangeStartMs = timeRangeStart;
        }
    }

    internal record TempVideo(VideoId Id, string Path);

    public class YoutubeClient
    {
        private YoutubeExplode.YoutubeClient _client;
        private ILogger _logger;
        private string _tempPath;
        private string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tempPath))
                {
                    _tempPath = Path.GetTempPath();
                }

                return _tempPath;
            }
        }
        
        public YoutubeClient(YoutubeExplode.YoutubeClient client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public IProgress DefaultProgressReporter { get; set; }

        public async Task<Video> GetVideoInfoAsync(VideoId id)
        {
            return await _client.Videos.GetAsync(id);
        }
        
        public async Task<Playlist> GetPlaylistInfoAsync(PlaylistId id)
        {
            return await _client.Playlists.GetAsync(id);
        }

        public IAsyncEnumerable<PlaylistVideo> GetPlaylistVideosInfoAsync(PlaylistId id)
        {
            return _client.Playlists.GetVideosAsync(id);
        }

        public async Task<string> DownloadVideo(VideoId id, IProgress progress = null)
        {
            var videoInfo = await _client.Videos.GetAsync(id);
            return await DownloadVideo(videoInfo, progress);
        }

        public async Task<string> DownloadVideo(IVideo videoInfo, IProgress progress = null)
        {
            VideoId id = videoInfo.Id;
            StreamManifest manifest = await _client.Videos.Streams.GetManifestAsync(id);
            _logger.Info($"Downloading video {videoInfo.Title}...");
            
            if (manifest == null)
                throw new ArgumentException("no manifest found");

            var stream = manifest.GetAudioOnlyStreams().OrderBy(s => s.Bitrate).FirstOrDefault();
            if (stream == null)
                throw new ArgumentException("no audio stream found");

            var tempDir = Path.Combine(TempPath, Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            
            var videoPath = Path.Combine(tempDir, $"{videoInfo.Title.RemoveInvalidChars()}.{stream.Container.Name}");

            progress ??= DefaultProgressReporter;
            await _client.Videos.Streams.DownloadAsync(stream, videoPath, progress);
            _logger.Info("Completed");
            return videoPath;
        }

        internal async Task<IEnumerable<TempVideo>> DownloadPlaylist(IPlaylist info, IEnumerable<IVideoFilter> filters = null, IPlaylistProgress playlistProgress = null)
        {
            IAsyncEnumerable<PlaylistVideo> videos = _client.Playlists.GetVideosAsync(info.Id);
        
            // info ??= await _client.Playlists.GetAsync(id);
            //_logger.Info($"{videos.Count()} videos found in playlist {info.Title}");
            var tempVideoPaths = new List<TempVideo>();
            await foreach (PlaylistVideo video in videos)
            {
                if (filters != null && filters.Any(f => f.ShouldFilter(video)))
                    continue;

                //_logger.Info($"{i+1}/{videos.Count}");
                IProgress downloadProgress = null;
                if (playlistProgress != null && playlistProgress.TryGetValue(video.Id, out IVideoProgress videoProgress))
                    downloadProgress = videoProgress?.DownloadProgress;

                tempVideoPaths.Add(new TempVideo(video.Id, await DownloadVideo(video, downloadProgress)));
            }

            return tempVideoPaths;
        }
        
        internal async Task<List<Chapter>> GetChaptersAsync(VideoId videoId)
        {
            try
            {
                var watchPageDoc = await GetHtmlWatchPage(videoId);
                return TryGetChapters(watchPageDoc);
            }
            catch { }

            try
            {
                return await TryGetChaptersFromDescription(videoId);
            }
            catch (Exception ex)
            {
                _logger.Info("Getting chapters failed");
                _logger.Info(ex.Message);                
                throw;
            }
            
            //TODO : try parse chapters comments
        }

        private async Task<List<Chapter>> TryGetChaptersFromDescription(VideoId id)
        {
            Video videoInfo = await _client.Videos.GetAsync(id);
            var desc = videoInfo.Description;
            var regex = new Regex("[0-9]?[0-9]?:?[0-5]?[0-9]:[0-5][0-9]", RegexOptions.Compiled);

            var descLines = desc.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            var chapters = new List<Chapter>();
            foreach (string line in descLines)
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    var title = line.Replace(match.Value, "").TrimStart().TrimEnd();
                    var matchValue = match.Value;
                    if (matchValue.Count(c => c == ':') == 1) 
                        matchValue = "00:" + matchValue;

                    var startTime = (ulong)TimeSpan.Parse(matchValue).TotalMilliseconds;
                    chapters.Add(new Chapter(title, startTime));
                }
            }
            
            return chapters;
        }
        
        private static List<Chapter> TryGetChapters(IHtmlDocument watchPageDoc)
        {
            var ytInitialData = watchPageDoc
                .GetElementsByTagName("script")
                .Select(e => e.Text())
                .FirstOrDefault(s => s.Contains("ytInitialData"));

            var json = Regex.Match(ytInitialData, "ytInitialData\\s*=\\s*(.+?})(?:\"\\))?;", RegexOptions.Singleline).Groups[1]
                .Value;

            using var doc = JsonDocument.Parse(json);
            var jsonDocument = doc.RootElement.Clone();
            // ReSharper disable once HeapView.BoxingAllocation
            var chaptersArray = jsonDocument
                .GetProperty("playerOverlays")
                .GetProperty("playerOverlayRenderer")
                .GetProperty("decoratedPlayerBarRenderer")
                .GetProperty("decoratedPlayerBarRenderer")
                .GetProperty("playerBar")
                .GetProperty("chapteredPlayerBarRenderer")
                .GetProperty("chapters")
                .EnumerateArray()
                .Select(j => new Chapter(
                    j.GetProperty("chapterRenderer").GetProperty("title").GetProperty("simpleText").GetString(),
                    j.GetProperty("chapterRenderer").GetProperty("timeRangeStartMillis").GetUInt64()));

            return chaptersArray.ToList();
        }

        private async Task<IHtmlDocument> GetHtmlWatchPage(VideoId videoId)
        {
            var videoController = typeof(VideoClient).GetField("_controller",
                BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_client.Videos);

            var assembly = typeof(YoutubeExplode.YoutubeClient).Assembly;
            var controllerType = assembly.GetType("YoutubeExplode.Videos.VideoController");
            var methodInfo = controllerType.GetMethod("GetVideoWatchPageAsync");
                        
            var watchPageExtractor = await methodInfo.InvokeAsync(null, new object[] {videoController, videoId.ToString()});
            var root = (IHtmlDocument)watchPageExtractor.GetType()
                .GetField("_content", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(watchPageExtractor);
            return root;
        }
    }
}
