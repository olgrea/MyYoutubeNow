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
using Xabe.FFmpeg;

namespace MyYoutubeNow.Converters
{
    class FFmpegWrapper
    {
        private const string GithubReleaseUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
        private readonly string _exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        private string _baseDirectory;

        private string _tempPath;
        private string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tempPath))
                {
                    _tempPath = Path.GetTempPath();
                    Directory.CreateDirectory(_tempPath);
                }

                return _tempPath;
            }
        }

        public FFmpegWrapper(string baseDir)
        {
            _baseDirectory = baseDir;

            if (!File.Exists(_exePath))
            {
                Console.WriteLine("FFmpeg not found.");
                var t = Download();
                t.Wait();
            }
            FFmpeg.SetExecutablesPath(_baseDirectory);
        }

        ~FFmpegWrapper()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        public async Task<string> ConvertToMp3(string videoPath, string outputPath)
        {
            IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(videoPath);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.mp3);

            IConversion conversion = FFmpeg.Conversions.New();
            conversion.AddStream(audioStream)
                .SetOverwriteOutput(true)
                .AddParameter(MakeQualityParam())
                .SetOutput(outputPath)
                ;

            Console.WriteLine($"Converting {Path.GetFileNameWithoutExtension(outputPath)} to mp3...");
            return await DoConversion(conversion);
        }

        public async Task VideoPartToMp3(string videoMixPath, string outputPath, ulong start, ulong end, string title)
        {
            IConversion conversion = FFmpeg.Conversions.New();
            IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(videoMixPath);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.mp3);

            conversion.AddStream(audioStream)
                .SetOverwriteOutput(true)
                .SetOutput(outputPath)
                .AddParameter(FFmpegWrapper.MakeSplitParam(start, end))
                .AddParameter(FFmpegWrapper.MakeFadeOutParam())
                ;

            var b = conversion.Build();
            Console.WriteLine($"Converting chapter {title} to mp3...");
            await DoConversion(conversion);
        }

        public async Task<string> VideosToSingleMp3(IEnumerable<string> videoPaths, string outputPath)
        {
            IConversion conversion = FFmpeg.Conversions.New();

            var concatParam = FFmpegWrapper.MakeConcatParam(videoPaths, outputPath);
            conversion.AddParameter(concatParam)
                .SetOverwriteOutput(true)
                .AddParameter(MakeQualityParam())
                .SetOutput(outputPath)
                ;

            Console.WriteLine($"Concatenating {videoPaths.Count()} files...");
            return await DoConversion(conversion);
        }

        private static string MakeQualityParam()
        {
            return "-q:a 2";
        }

        private static async Task<string> DoConversion(IConversion conversion)
        {
            var cmd = conversion.Build();
            var nbInputs = cmd.Split("-i").Count() - 1;

            using var convProgress = new InlineProgress();
            conversion.OnProgress += (sender, args) =>
            {
                convProgress.Report(args.Duration.TotalMilliseconds / (args.TotalLength.TotalMilliseconds * nbInputs));

            };
            await conversion.Start();

            var outputFileInfo = new FileInfo(conversion.OutputFilePath);
            if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            return conversion.OutputFilePath;
        }

        private static string MakeSplitParam(ulong start, ulong end)
        {
            var sb = new StringBuilder();
            sb.Append($"-ss {TimeSpan.FromMilliseconds(start).ToFFmpeg()} ");
            if (end > 0)
            {
                sb.Append($"-to {TimeSpan.FromMilliseconds(end).ToFFmpeg()} ");
            }

            return sb.ToString();
        }

        private static string MakeFadeOutParam()
        {
            return "-filter_complex \"aevalsrc=0:d=1.0 [a_silence]; [0:a:0] [a_silence] acrossfade=d=1.0\" ";
        }

        private static string MakeConcatParam(IEnumerable<string> pathsToMerge, string outputFilePath)
        {
            var sb = new StringBuilder();
            var count = pathsToMerge.Count();

            // Input parameters
            foreach (var path in pathsToMerge)
                sb.Append($"-i \"{path}\" ");

            // filter
            sb.Append($"-filter_complex \"");
            for (int i = 0; i < count; i++)
                sb.Append($"[{i}:a:0]");
            sb.Append($"concat=n={count}:v=0:a=1[outa]\" ");

            // map
            sb.Append($"-map \"[outa]\" \"{outputFilePath}\" ");
            return sb.ToString();
        }

        public async Task Download()
        {
            Console.WriteLine("Downloading FFmpeg...");
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

            using (var progress = new InlineProgress())
            {
                await httpClient.DownloadAsync(releaseUrl, zipPath, elem.GetProperty("size").GetInt64(), progress);
            }

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
                File.Copy(file, Path.Combine(_baseDirectory, Path.GetFileName(file)));
            }

            if (File.Exists(_exePath))
            {
                File.Delete(zipPath);
                Directory.Delete(extractedDir, true);
            }
        }
    }
}