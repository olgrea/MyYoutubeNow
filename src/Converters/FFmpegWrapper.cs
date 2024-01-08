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
using Instances;
using NLog;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")] 

namespace MyYoutubeNow.Converters
{
    internal class FFmpegWrapper
    {
        private const string GithubReleaseUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
        internal const string FFmpegExeName = "ffmpeg.exe";
        private readonly string _exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FFmpegExeName);
        private string _baseDirectory;
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

        public FFmpegWrapper(string baseDir, ILogger logger)
        {
            _logger = logger;
            _baseDirectory = baseDir;
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _baseDirectory, TemporaryFilesFolder = TempPath });
        }

        ~FFmpegWrapper()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        public IProgressReport ProgressReport { get; set; }

        public async Task<bool> ConvertToMp3(string videoPath, string outputPath)
        {
            if(!FFmpegFound)
            {
                _logger.Info("FFmpeg not found.");
                await Download();
            }

            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var audioStream = mediaInfo.PrimaryAudioStream;

            _logger.Info($"Converting {Path.GetFileNameWithoutExtension(outputPath)} to mp3...");

            try
            {
                var task =  await FFMpegArguments
                    .FromFileInput(videoPath)
                    .OutputToFile(outputPath, true, op =>
                        op
                        .SelectStream(audioStream.Index)
                        .WithAudioCodec("mp3")
                        .DisableChannel(FFMpegCore.Enums.Channel.Video)
                        .WithCustomArgument(MakeQualityParam())
                        .OverwriteExisting()
                    )
                    .NotifyOnProgress(progress =>
                    {
                        ProgressReport?.Report(progress/100d);
                    }, audioStream.Duration)
                    .ProcessAsynchronously();

                _logger.Info("Completed");
                return task;
            }
            finally
            {
                var outputFileInfo = new FileInfo(outputPath);
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                    Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            }
        }

        public async Task<bool> VideoPartToMp3(string videoMixPath, string outputPath, ulong start, ulong end, string title)
        {
            if (!FFmpegFound)
            {
                _logger.Info("FFmpeg not found.");
                await Download();
            }

            var mediaInfo = await FFProbe.AnalyseAsync(videoMixPath);
            var audioStream = mediaInfo.PrimaryAudioStream;

            _logger.Info($"Converting chapter {title} to mp3...");

            try
            {
                var task = await FFMpegArguments
                    .FromFileInput(videoMixPath)
                    .OutputToFile(outputPath, true, op =>
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
                        ProgressReport?.Report(progress.TotalMilliseconds / (double)(end-start));
                    })
                    .ProcessAsynchronously();
                
                _logger.Info("Completed");
                return task;
            }
            finally
            {
                var outputFileInfo = new FileInfo(outputPath);
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                    Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            }

        }

        public async Task<bool> VideosToSingleMp3(IEnumerable<string> videoPaths, string outputPath)
        {
            if (!FFmpegFound)
            {
                _logger.Info("FFmpeg not found.");
                await Download();
            }

            _logger.Info($"Concatenating {videoPaths.Count()} files...");

            try
            {
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
                    .OutputToFile(outputPath, true, op =>
                        op
                        .WithAudioCodec("mp3")
                        .WithCustomArgument(MakeConcatParam(paths.Count, outputPath))
                        .OverwriteExisting()
                    )
                    .NotifyOnProgress(progress =>
                    {
                        ProgressReport?.Report(progress.TotalMilliseconds / totalDuration);
                    })
                    .ProcessAsynchronously();

                _logger.Info("Completed");
                return task;
            }
            finally 
            {
                var outputFileInfo = new FileInfo(outputPath);
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                    Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            }
        }

        private bool FFmpegFound => File.Exists(_exePath);

        private static string MakeQualityParam()
        {
            return "-q:a 2";
        }

        private static string MakeSplitParam(ulong start, ulong end)
        {
            var sb = new StringBuilder();
            sb.Append($"-ss {ToFFmpeg(TimeSpan.FromMilliseconds(start))} ");
            if (end > 0)
            {
                sb.Append($"-to {ToFFmpeg(TimeSpan.FromMilliseconds(end))} ");
            }

            return sb.ToString();
        }

        static string ToFFmpeg(TimeSpan ts)
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

        public async Task Download()
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
            var zipPath = Path.Combine(_baseDirectory, zipFileName);

            await httpClient.DownloadAsync(releaseUrl, zipPath, elem.GetProperty("size").GetInt64(), ProgressReport);
            _logger.Info("Completed");

            var extractedDir = zipPath.Replace(".zip", "");
            if (Directory.Exists(extractedDir))
                Directory.Delete(extractedDir, true);

            await using (FileStream zipStream = File.OpenRead(zipPath))
            {
                var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
                zip.ExtractToDirectory(_baseDirectory);
            }

            foreach (string file in Directory.EnumerateFiles(extractedDir, "*.exe", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(_baseDirectory, Path.GetFileName(file)), overwrite: true);
            }

            if (File.Exists(_exePath))
            {
                File.Delete(zipPath);
                Directory.Delete(extractedDir, true);
            }
        }
    }
}