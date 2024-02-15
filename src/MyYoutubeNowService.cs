using System.IO;
using System.Threading.Tasks;

using MyYoutubeNow.Converters;
using MyYoutubeNow.Client;
using MyYoutubeNow.Utils;

using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;
using NLog;
using System;
using Microsoft.Extensions.DependencyInjection;
using NLog.Layouts;
using NLog.Config;
using System.Collections.Generic;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Options;
using System.Linq;

namespace MyYoutubeNow
{
    public class MyYoutubeNowService
    {
        YoutubeClient _client;
        MediaConverter _converter;
        IServiceProvider _services;
        LoggingConfiguration _loggingConfig;

        public MyYoutubeNowService(IProgress progressReport = null)
        {
            _loggingConfig = ConfigureLogger();
            _services = ConfigureServices();

            _client = _services.GetService<YoutubeClient>();
            _converter = _services.GetService<MediaConverter>();

            _client.DefaultProgressReporter = _converter.DefaultProgressReport = progressReport;
        }

        public string OutputDir { get; set; } = "output";

        public LoggingConfiguration LoggingConfig => _loggingConfig;

        static public bool IsVideo(string url) => VideoId.TryParse(url) != null;
        static public bool IsPlaylist(string url) => PlaylistId.TryParse(url) != null;

        public async Task<IVideo> GetVideoInfoAsync(string url)
        {
            VideoId id = VideoId.Parse(url);
            return await _client.GetVideoInfoAsync(id);
        }

        public async Task<IPlaylist> GetPlaylistInfoAsync(string url)
        {
            PlaylistId id = PlaylistId.Parse(url);
            return await _client.GetPlaylistInfoAsync(id);
        }

        public IAsyncEnumerable<PlaylistVideo> GetPlaylistVideosInfoAsync(string url)
        {
            PlaylistId id = PlaylistId.Parse(url);
            return _client.GetPlaylistVideosInfoAsync(id);
        }

        public async Task DownloadAndConvertVideo(string url, IVideoProgress videoProgress = null)
        {
            await DownloadAndConvertVideo(url, new VideoOptions(), videoProgress);
        }

        public async Task DownloadAndConvertVideo(string url, IVideoOptions options, IVideoProgress videoProgress = null)
        {
            VideoId id = VideoId.Parse(url);
            Video info = await _client.GetVideoInfoAsync(id);
            await DownloadAndConvertVideo(info, options, videoProgress);
        }

        public async Task DownloadAndConvertVideo(IVideo info, IVideoProgress videoProgress = null)
        {
            await DownloadAndConvertVideo(info, new VideoOptions(), videoProgress);
        }

        public async Task DownloadAndConvertVideo(IVideo info, IVideoOptions options, IVideoProgress videoProgress = null)
        {
            var videoPath = await _client.DownloadVideo(info, videoProgress?.DownloadProgress);
            if (options.Split)
            {
                var chapters = await _client.GetChaptersAsync(info.Id);
                await _converter.ConvertToMp3s(videoPath, chapters, OutputDir, videoProgress?.ConvertProgress);
            }
            else
            {
                await _converter.ConvertToMp3(videoPath, OutputDir, videoProgress?.ConvertProgress);
            }
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }

        public async Task DownloadAndConvertPlaylist(string url, IPlaylistProgress playlistProgress = null)
        {
            await DownloadAndConvertPlaylist(url, new PlaylistOptions(), playlistProgress);
        }

        public async Task DownloadAndConvertPlaylist(string url, IPlaylistOptions playlistOptions, IPlaylistProgress playlistProgress = null)
        {
            PlaylistId id = PlaylistId.Parse(url);
            Playlist info = await _client.GetPlaylistInfoAsync(id);
            await ConvertPlaylist(info, playlistOptions, playlistProgress);
        }

        public async Task ConvertPlaylist(IPlaylist info, IPlaylistProgress playlistProgress = null)
        {
            await ConvertPlaylist(info, new PlaylistOptions(), playlistProgress);
        }

        public async Task ConvertPlaylist(IPlaylist info, IPlaylistOptions playlistOptions, IPlaylistProgress playlistProgress = null)
        {
            if (playlistOptions.Concatenate)
            {
                await DownloadAllThenConvert(info, playlistOptions, playlistProgress);
                return;
            }
            
            IAsyncEnumerable<PlaylistVideo> videos = _client.GetPlaylistVideosInfoAsync(info.Id);

            var filters = playlistOptions.Filters;
            await foreach (PlaylistVideo video in videos)
            {
                if (filters != null && filters.Any(f => f.ShouldFilter(video)))
                    continue;

                //_logger.Info($"{i+1}/{videos.Count}");
                IVideoProgress progress = null;
                playlistProgress?.VideoProgresses.TryGetValue(video.Id, out progress);

                await DownloadAndConvertVideo(video, progress);
            }
        }

        async Task DownloadAllThenConvert(IPlaylist info, IPlaylistOptions playlistOptions, IPlaylistProgress playlistProgress = null)
        {
            IEnumerable<TempVideo> tempVideoPaths = await _client.DownloadPlaylist(info, playlistOptions.Filters, playlistProgress);

            Dictionary<TempVideo, IProgress> progressDict = null;
            if(playlistProgress != null)
            {
                progressDict = new();
                foreach (TempVideo tempVideo in tempVideoPaths)
                    progressDict.Add(tempVideo, playlistProgress.VideoProgresses[tempVideo.Id].ConvertProgress);
            }
            
            await _converter.ConcatenateMp3s(tempVideoPaths, OutputDir, $"{info.Title.RemoveInvalidChars()}", progressDict);

            foreach (var tempVideo in tempVideoPaths)
            {
                if (File.Exists(tempVideo.Path))
                    File.Delete(tempVideo.Path);
            }
        }

        private LoggingConfiguration ConfigureLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var logdebugger = new NLog.Targets.DebuggerTarget("logdebugger");
            logdebugger.Layout = Layout.FromString("${message:withexception=true}");

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logdebugger);
            config.Apply();
            return config;
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(typeof(ILogger), LogManager.GetLogger($"logdebugger"));
            services.AddSingleton<YoutubeExplode.YoutubeClient, YoutubeExplode.YoutubeClient>();
            services.AddSingleton<YoutubeClient, YoutubeClient>();
            services.AddSingleton<MediaConverter, MediaConverter>();

            return services.BuildServiceProvider();
        }
    }
}
