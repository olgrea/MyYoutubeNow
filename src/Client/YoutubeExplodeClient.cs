using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using MyYoutubeNow.Converters;
using MyYoutubeNow.Options.Filters;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Utils;
using NLog;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace MyYoutubeNow.Client
{
    public class VideoSegment : IVideoSegment
    {
        public string Title { get; }
        public ulong SegmentStartTimeMs { get; }
        public VideoSegment(string title, ulong timeRangeStart)
        {
            Title = title;
            SegmentStartTimeMs = timeRangeStart;
        }

        public VideoSegment(Chapter chapter)
        {
            Title = chapter.Title;
            SegmentStartTimeMs = chapter.SegmentStartTimeMs;
        }
    }

    abstract class VideoBase
    {
        protected IVideo _video;

        protected VideoBase(IVideo video)
        {
            _video = video;
        }

        public string Id => _video.Id;
        public string Title => _video.Title;
        public TimeSpan? Duration => _video.Duration;
        public string ThumbnailUrl => _video.Thumbnails.FirstOrDefault()?.Url;

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object obj) => obj is VideoBase vb && vb.Id.Equals(Id);
    }

    class Video : VideoBase, IVideoInfo
    {
        public Video(YoutubeExplode.Videos.Video video) : base(video) {}

        public string Description => (_video as YoutubeExplode.Videos.Video).Description;
        public static implicit operator Video(YoutubeExplode.Videos.Video v) => new Video(v);
    }

    class PlaylistVideo : VideoBase, IPlaylistVideoInfo
    {
        public PlaylistVideo(YoutubeExplode.Playlists.PlaylistVideo video) : base(video) {}

        public string PlaylistId => (_video as YoutubeExplode.Playlists.PlaylistVideo).PlaylistId;
        public static implicit operator PlaylistVideo(YoutubeExplode.Playlists.PlaylistVideo v) => new PlaylistVideo(v);
    }

    class Playlist : IPlaylistInfo
    {
        YoutubeExplode.Playlists.Playlist _playlist;

        public Playlist(YoutubeExplode.Playlists.Playlist pl)
        {
            _playlist = pl;
        }

        public string Id => _playlist.Id;
        public string Title => _playlist.Title;
        public static implicit operator Playlist(YoutubeExplode.Playlists.Playlist pl) => new Playlist(pl);
        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object obj) => obj is Playlist info && info.Id.Equals(Id);
    }

    public class PlaylistUnavailableException : Exception
    {
        public PlaylistUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }

    internal class YoutubeExplodeClient : IYoutubeClient
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
        
        public YoutubeExplodeClient(YoutubeExplode.YoutubeClient client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public IProgress DefaultProgressReport { get; set; }

        public bool IsVideo(string url) => VideoId.TryParse(url) != null;

        public bool IsPlaylist(string url) => PlaylistId.TryParse(url) != null;

        public async Task<IVideoInfo> GetVideoInfoAsync(string url)
        {
            VideoId id = VideoId.Parse(url);
            return (Video)await _client.Videos.GetAsync(id);
        }
        
        public async Task<IPlaylistInfo> GetPlaylistInfoAsync(string url)
        {
            PlaylistId id = PlaylistId.Parse(url);
            try
            {
                return (Playlist)await _client.Playlists.GetAsync(id);
            }
            catch(YoutubeExplode.Exceptions.PlaylistUnavailableException ex)
            {
                throw new PlaylistUnavailableException(ex.Message, ex);
            }
        }

        public async IAsyncEnumerable<IPlaylistVideoInfo> GetPlaylistVideosInfoAsync(string url)
        {
            PlaylistId id = PlaylistId.Parse(url);

            await foreach (YoutubeExplode.Playlists.PlaylistVideo vid in _client.Playlists.GetVideosAsync(id).ConfigureAwait(false))
            {
                yield return (PlaylistVideo)vid;
            }
        }

        public async Task<string> DownloadVideo(string url, IProgress progress = null)
        {
            var id = VideoId.Parse(url);
            var videoInfo = await _client.Videos.GetAsync(id);
            return await DownloadVideo((Video)videoInfo, progress);
        }

        public async Task<string> DownloadVideo(IVideoInfo videoInfo, IProgress progress = null)
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

            progress ??= DefaultProgressReport;
            await _client.Videos.Streams.DownloadAsync(stream, videoPath, progress);
            _logger.Info("Completed");
            return videoPath;
        }

        public async Task<IEnumerable<DownloadedVideo>> DownloadPlaylist(IPlaylistInfo info, IEnumerable<IVideoFilter> filters = null, IPlaylistProgress playlistProgress = null)
        {
            IAsyncEnumerable<YoutubeExplode.Playlists.PlaylistVideo> videos = _client.Playlists.GetVideosAsync(info.Id);
        
            // info ??= await _client.Playlists.GetAsync(id);
            //_logger.Info($"{videos.Count()} videos found in playlist {info.Title}");
            var tempVideoPaths = new List<DownloadedVideo>();
            await foreach (PlaylistVideo video in videos)
            {
                if (filters != null && filters.Any(f => f.ShouldFilter(video)))
                    continue;

                //_logger.Info($"{i+1}/{videos.Count}");
                IProgress downloadProgress = null;
                if (playlistProgress != null && playlistProgress.VideoProgresses.TryGetValue(video, out IVideoProgress videoProgress))
                    downloadProgress = videoProgress?.DownloadProgress;

                tempVideoPaths.Add(new DownloadedVideo(video, await DownloadVideo(video, downloadProgress)));
            }

            return tempVideoPaths;
        }
        
        public async Task<IEnumerable<Chapter>> GetChaptersAsync(IVideoInfo video)
        {
            try
            {
                var watchPageDoc = await GetHtmlWatchPage(video.Id);
                return TryGetChapters(watchPageDoc);
            }
            catch { }

            try
            {
                return await GetChaptersFromDescription(video.Id);
            }
            catch (Exception ex)
            {
                _logger.Info("Getting chapters failed");
                _logger.Info(ex.Message);                
                throw;
            }
            
            //TODO : try parse chapters comments
        }

        private async Task<List<Chapter>> GetChaptersFromDescription(VideoId id)
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
