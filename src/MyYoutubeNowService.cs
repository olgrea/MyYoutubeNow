using System.IO;
using System.Threading.Tasks;

using MyYoutubeNow.Converters;
using MyYoutubeNow.Client;
using MyYoutubeNow.Utils;

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
        IYoutubeClient _client;
        IMediaConverter _converter;
        IServiceProvider _services;
        LoggingConfiguration _loggingConfig;

        public MyYoutubeNowService(IProgress progressReport = null)
        {
            _loggingConfig = ConfigureLogger();
            _services = ConfigureServices();

            _client = _services.GetService<IYoutubeClient>();
            _converter = _services.GetService<IMediaConverter>();

            _client.DefaultProgressReport = _converter.DefaultProgressReport = progressReport;
        }

        public string OutputDir { get => _converter.OutputDir; set => _converter.OutputDir = value; }

        public LoggingConfiguration LoggingConfig => _loggingConfig;

        public bool IsVideo(string url) => _client.IsVideo(url);
        public bool IsPlaylist(string url) => _client.IsPlaylist(url);

        public async Task<IVideoInfo> GetVideoInfoAsync(string url)
        {
            return await _client.GetVideoInfoAsync(url);
        }

        public async Task<IPlaylistInfo> GetPlaylistInfoAsync(string url)
        {
            return await _client.GetPlaylistInfoAsync(url);
        }

        public IAsyncEnumerable<IPlaylistVideoInfo> GetPlaylistVideosInfoAsync(string url)
        {
            return _client.GetPlaylistVideosInfoAsync(url);
        }

        public async Task<string> DownloadVideo(string url, IProgress progress = null)
        {
            IVideoInfo info = await _client.GetVideoInfoAsync(url);
            return await DownloadVideo(info, progress);
        }

        public async Task<string> DownloadVideo(string url, IVideoOptions options, IProgress progress = null)
        {
            IVideoInfo info = await _client.GetVideoInfoAsync(url);
            return await DownloadVideo(info, options, progress);
        }

        public async Task<string> DownloadVideo(IVideoInfo info, IProgress progress = null)
        {
            return await DownloadVideo(info, new VideoOptions(), progress);
        }

        public async Task<string> DownloadVideo(IVideoInfo info, IVideoOptions options, IProgress progress = null)
        {
            var videoPath = await _client.DownloadVideo(info, progress);
            return videoPath;
        }

        public async Task DownloadAndConvertVideo(string url, IVideoProgress videoProgress = null)
        {
            await DownloadAndConvertVideo(url, new VideoOptions(), videoProgress);
        }

        public async Task DownloadAndConvertVideo(string url, IVideoOptions options, IVideoProgress videoProgress = null)
        {
            IVideoInfo info = await _client.GetVideoInfoAsync(url);
            await DownloadAndConvertVideo(info, options, videoProgress);
        }

        public async Task DownloadAndConvertVideo(IVideoInfo info, IVideoProgress videoProgress = null)
        {
            await DownloadAndConvertVideo(info, new VideoOptions(), videoProgress);
        }

        public async Task DownloadAndConvertVideo(IVideoInfo info, IVideoOptions options, IVideoProgress videoProgress = null)
        {
            var videoPath = await DownloadVideo(info, videoProgress?.DownloadProgress);
            if (options.Split)
            {
                var chapters = await _client.GetChaptersAsync(info);
                await _converter.ConvertVideoToMultipleMp3s(videoPath, chapters.Cast<VideoSegment>(), videoProgress?.ConvertProgress);
            }
            else
            {
                await _converter.ConvertVideoToMp3(videoPath, null, videoProgress?.ConvertProgress);
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
            IPlaylistInfo info = await _client.GetPlaylistInfoAsync(url);
            await DownloadAndConvertPlaylist(info, playlistOptions, playlistProgress);
        }

        public async Task DownloadAndConvertPlaylist(IPlaylistInfo info, IPlaylistProgress playlistProgress = null)
        {
            await DownloadAndConvertPlaylist(info, new PlaylistOptions(), playlistProgress);
        }

        public async Task DownloadAndConvertPlaylist(IPlaylistInfo info, IPlaylistOptions playlistOptions, IPlaylistProgress playlistProgress = null)
        {
            if (playlistOptions.Concatenate)
            {
                await DownloadAllThenConvert(info, playlistOptions, playlistProgress);
                return;
            }
            
            IAsyncEnumerable<IPlaylistVideoInfo> videos = _client.GetPlaylistVideosInfoAsync(info.Id);

            var filters = playlistOptions.Filters;
            List<Task> tasks = new();
            await foreach (PlaylistVideo video in videos)
            {
                if (filters != null && filters.Any(f => f.ShouldFilter(video)))
                    continue;

                //_logger.Info($"{i+1}/{videos.Count}");
                IVideoProgress progress = null;
                playlistProgress?.VideoProgresses.TryGetValue(video, out progress);

                tasks.Add(Task.Run(async () =>
                {
                    var videoPath = await DownloadVideo(video, progress?.DownloadProgress);
                    var fileName = Path.Combine(info.Title.RemoveInvalidChars(), video.Title.RemoveInvalidChars() + ".mp3");
                    await _converter.ConvertVideoToMp3(videoPath, fileName, progress?.ConvertProgress);

                    if (File.Exists(videoPath))
                        File.Delete(videoPath);
                }));
            }

            await Task.WhenAll(tasks);
        }

        async Task DownloadAllThenConvert(IPlaylistInfo info, IPlaylistOptions playlistOptions, IPlaylistProgress playlistProgress = null)
        {
            var tempVideoPaths = await _client.DownloadPlaylist(info, playlistOptions.Filters, playlistProgress);

            Dictionary<string, IProgress> progressDict = null;
            if(playlistProgress != null)
            {
                progressDict = new();
                foreach (var tempVideo in tempVideoPaths)
                {
                    progressDict.Add(tempVideo.Path, playlistProgress.VideoProgresses[tempVideo.VideoInfo].ConvertProgress);
                }
            }
            
            await _converter.ConvertVideosToSingleMp3(tempVideoPaths.Select(vp => vp.Path), $"{info.Title.RemoveInvalidChars()}", progressDict);

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
            services.AddSingleton<IYoutubeClient, YoutubeExplodeClient>();
            services.AddSingleton<IMediaConverter, FFmpegMediaConverter>();

            return services.BuildServiceProvider();
        }
    }
}
