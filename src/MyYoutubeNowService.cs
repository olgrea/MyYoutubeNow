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

namespace MyYoutubeNow
{
    public class MyYoutubeNowService
    {
        YoutubeClient _client;
        MediaConverter _converter;
        IServiceProvider _services;
        LoggingConfiguration _loggingConfig;

        public MyYoutubeNowService(IProgressReport progressReport = null)
        {
            _loggingConfig = ConfigureLogger();
            _services = ConfigureServices();

            _client = _services.GetService<YoutubeClient>();
            _converter = _services.GetService<MediaConverter>();

            _client.ProgressReport = _converter.ProgressReport = progressReport;
        }

        public string OutputDir = AppDomain.CurrentDomain.BaseDirectory;

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

        public async Task ConvertVideo(string url, bool split = false)
        {
            VideoId id = VideoId.Parse(url);
            Video info = await _client.GetVideoInfoAsync(id);
            await ConvertVideo(info, split);
        }

        public async Task ConvertVideo(IVideo info, bool split = false)
        {
            var videoPath = await _client.DownloadVideo(info.Id, info);
            if (split)
            {
                var chapters = await _client.GetChaptersAsync(info.Id);
                await _converter.ConvertToMp3s(videoPath, chapters, info.Title.RemoveInvalidChars());
            }
            else
            {
                await _converter.ConvertToMp3(videoPath);
            }
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }

        public async Task ConvertPlaylist(string url, bool concatenate = false)
        {
            PlaylistId id = PlaylistId.Parse(url);
            Playlist info = await _client.GetPlaylistInfoAsync(id);
            await ConvertPlaylist(info, null, concatenate);
        }

        public async Task ConvertPlaylist(IPlaylist info, IEnumerable<IPlaylistVideoFilter> filters = null,  bool concatenate = false)
        {
            var videoPaths = await _client.DownloadPlaylist(info, filters);

            if (concatenate)
            {
                await _converter.ConcatenateMp3s(videoPaths, $"{info.Title.RemoveInvalidChars()}");
            }
            else
            {
                await _converter.ConvertToMp3(videoPaths, $"{info.Title.RemoveInvalidChars()}");
            }
            foreach (var path in videoPaths)
            {
                if (File.Exists(path))
                    File.Delete(path);
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
