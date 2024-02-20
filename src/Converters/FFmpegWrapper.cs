using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MyYoutubeNow.Utils;
using FFMpegCore;
using NLog;
using MyYoutubeNow.Progress;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")] 

namespace MyYoutubeNow.Converters
{
    internal class FFmpegWrapper
    {
        internal class Options
        {
            public string BinaryFolder { get; set; }
            public string OutputDir { get; set; }
            public bool AutoDownloadBinaries { get; set; }
        }

        const string GithubReleaseUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
        internal const string FFmpegExeName = "ffmpeg.exe";
        string ExePath => Path.Combine(_options.BinaryFolder, FFmpegExeName);
        Options _options;
        ILogger _logger;

        private string _tempPath;
        private string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tempPath))
                {
                    _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    Directory.CreateDirectory(_tempPath);
                }

                return _tempPath;
            }
        }

        internal string OutputDir
        {
            get => _options.OutputDir;
            set
            {
                CheckPathValidity(value, nameof(OutputDir));
                _options.OutputDir = value;
            }
        }

        public FFmpegWrapper(ILogger logger)
            : this(new Options(), logger)
        {

        }

        public FFmpegWrapper(Options options, ILogger logger)
        {
            _options = options;

            CheckPathValidity(_options.BinaryFolder, nameof(_options.BinaryFolder));
            CheckPathValidity(_options.OutputDir, nameof(_options.OutputDir));

            if (!_options.AutoDownloadBinaries && !File.Exists(ExePath))
                throw new FileNotFoundException($"FFmpeg not found at \"{_options.BinaryFolder}\". Set {nameof(_options.AutoDownloadBinaries)} to automatically download them.");

            _logger = logger;
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _options.BinaryFolder, TemporaryFilesFolder = TempPath });
        }


        ~FFmpegWrapper()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        public IProgress DefaultProgressReport { get; set; }

        private void CheckPathValidity(string path, string name)
        {
            try
            {
                Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid {name} path : {path}.", ex);
            }
        }

        private async Task CheckFFmpegExist()
        {
            if(!File.Exists(ExePath))
            {
                _logger.Info("FFmpeg not found.");
                if (_options.AutoDownloadBinaries)
                    await DownloadBinaries();
                else
                    throw new FileNotFoundException($"FFmpeg not found at \"{_options.BinaryFolder}\". Set {nameof(_options.AutoDownloadBinaries)} to automatically download them.");
            }
        }

        public async Task<bool> ConvertToMp3(string videoPath, string fileName, IProgress progressReport = null)
        {
            await CheckFFmpegExist();

            fileName = Path.Combine(OutputDir, fileName);

            progressReport ??= DefaultProgressReport;
            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var audioStream = mediaInfo.PrimaryAudioStream;

            _logger.Info($"Converting {Path.GetFileNameWithoutExtension(fileName)} to mp3...");

            try
            {
                var task =  await FFMpegArguments
                    .FromFileInput(videoPath)
                    .OutputToFile(fileName, true, op =>
                        op
                        .SelectStream(audioStream.Index)
                        .WithAudioCodec("mp3")
                        .DisableChannel(FFMpegCore.Enums.Channel.Video)
                        .WithCustomArgument(MakeQualityParam())
                        .OverwriteExisting()
                    )
                    .NotifyOnProgress(progress =>
                    {
                        if (double.IsInfinity(progress))
                            progress = 0.0;

                        progressReport?.Report(progress/100d);
                    }, audioStream.Duration)
                    .ProcessAsynchronously();

                _logger.Info("Completed");
                return task;
            }
            finally
            {
                var outputFileInfo = new FileInfo(fileName);
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                    Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            }
        }

        public async Task<bool> VideoSegmentToMp3(string videoMixPath, ulong start, ulong end, string title, string fileName, IProgress progressReport = null)
        {
            await CheckFFmpegExist();

            fileName = Path.Combine(OutputDir, fileName);

            progressReport ??= DefaultProgressReport;
            var mediaInfo = await FFProbe.AnalyseAsync(videoMixPath);
            var audioStream = mediaInfo.PrimaryAudioStream;

            _logger.Info($"Converting chapter {title} to mp3...");

            try
            {
                var task = await FFMpegArguments
                    .FromFileInput(videoMixPath)
                    .OutputToFile(fileName, true, op =>
                        op
                        .SelectStream(audioStream.Index)
                        .WithAudioCodec("mp3")
                        .DisableChannel(FFMpegCore.Enums.Channel.Video)
                        .WithCustomArgument(MakeQualityParam())
                        .WithCustomArgument(MakeSplitParam(start, end))
                        .WithCustomArgument(MakeFadeOutParam())
                        .OverwriteExisting()
                    )
                    .NotifyOnProgress(progress =>
                    {
                        progressReport?.Report(progress.TotalMilliseconds / (double)(end-start));
                    })
                    .ProcessAsynchronously();
                
                _logger.Info("Completed");
                return task;
            }
            finally
            {
                var outputFileInfo = new FileInfo(fileName);
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                    Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            }

        }

        public async Task<bool> VideosToSingleMp3(IEnumerable<string> videoPaths, string fileName, IProgress progressReport = null)
        {
            await CheckFFmpegExist();

            fileName = Path.Combine(OutputDir, fileName);

            _logger.Info($"Concatenating {videoPaths.Count()} files...");

            try
            {
                progressReport ??= DefaultProgressReport;
                var paths = videoPaths.ToList();
                
                var mediaInfo = await FFProbe.AnalyseAsync(paths[0]);
                var totalDuration = mediaInfo.Duration.TotalMilliseconds;
                var ffmpegArgs = FFMpegArguments.FromFileInput(paths[0]);

                for (int i = 1; i < paths.Count; i++)
                {
                    mediaInfo = await FFProbe.AnalyseAsync(paths[i]);
                    totalDuration += mediaInfo.Duration.TotalMilliseconds;
                    ffmpegArgs.AddFileInput(paths[i]);
                }

                var task = await ffmpegArgs
                    .OutputToFile(fileName, true, op =>
                        op
                        .WithAudioCodec("mp3")
                        .WithCustomArgument(MakeConcatParam(paths.Count, fileName))
                        .OverwriteExisting()
                    )
                    .NotifyOnProgress(progress =>
                    {
                        progressReport?.Report(progress.TotalMilliseconds / totalDuration);
                    })
                    .ProcessAsynchronously();

                _logger.Info("Completed");
                return task;
            }
            finally 
            {
                var outputFileInfo = new FileInfo(fileName);
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                    Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            }
        }

        private static string MakeQualityParam()
        {
            return "-q:a 2";
        }

        private static string MakeSplitParam(ulong start, ulong end)
        {
            var sb = new StringBuilder();
            sb.Append($"-ss {ToFFmpegFormat(TimeSpan.FromMilliseconds(start))} ");
            if (end > 0)
            {
                sb.Append($"-to {ToFFmpegFormat(TimeSpan.FromMilliseconds(end))} ");
            }

            return sb.ToString();
        }

        static string ToFFmpegFormat(TimeSpan ts)
        {
            int milliseconds = ts.Milliseconds;
            int seconds = ts.Seconds;
            int minutes = ts.Minutes;
            int num = (int)ts.TotalHours;
            return $"{num:D}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }

        private static string MakeFadeOutParam()
        {
            return "-filter_complex \"aevalsrc=0:d=1.0 [a_silence]; [0:a:0] [a_silence] acrossfade=d=1.0\" ";
        }

        private static string MakeConcatParam(int nbToConcat, string outputFilePath)
        {
            var sb = new StringBuilder();

            //// Input parameters
            //foreach (var path in pathsToMerge)
            //    sb.Append($"-i \"{path}\" ");

            // filter
            sb.Append($"-filter_complex \"");
            for (int i = 0; i < nbToConcat; i++)
                sb.Append($"[{i}:a:0]");
            sb.Append($"concat=n={nbToConcat}:v=0:a=1[outa]\" ");

            // map
            sb.Append($"-map \"[outa]\" \"{outputFilePath}\" ");
            return sb.ToString();
        }

        async Task DownloadBinaries()
        {
            _logger.Info("Downloading FFmpeg...");
            using HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "request");

            var json = await httpClient.GetStringAsync(GithubReleaseUrl);
            using JsonDocument doc = JsonDocument.Parse(json);
            var jsonDocument = doc.RootElement.Clone();
            // ReSharper disable once HeapView.BoxingAllocation
            var elem = jsonDocument
                .EnumerateArray().First()
                .GetProperty("assets")
                .EnumerateArray()
                .FirstOrDefault(item => item.GetProperty("browser_download_url").GetString().EndsWith("win64-gpl.zip"));

            var releaseUrl = elem.GetProperty("browser_download_url").GetString();
            var zipFileName = Path.GetFileName(releaseUrl);

            if (!Directory.Exists(_options.BinaryFolder))
                Directory.CreateDirectory(_options.BinaryFolder);
            var zipPath = Path.Combine(_options.BinaryFolder, zipFileName);

            await httpClient.DownloadAsync(releaseUrl, zipPath, elem.GetProperty("size").GetInt64(), DefaultProgressReport);
            _logger.Info("Completed");

            var extractedDir = zipPath.Replace(".zip", "");
            if (Directory.Exists(extractedDir))
                Directory.Delete(extractedDir, true);

            await using (FileStream zipStream = File.OpenRead(zipPath))
            {
                var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
                zip.ExtractToDirectory(_options.BinaryFolder);
            }

            foreach (string file in Directory.EnumerateFiles(extractedDir, "*.exe", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(_options.BinaryFolder, Path.GetFileName(file)), overwrite: true);
            }

            if (File.Exists(ExePath))
            {
                File.Delete(zipPath);
                Directory.Delete(extractedDir, true);
            }
        }
    }
}