using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Utils;
using NLog;

namespace MyYoutubeNow.Converters
{
    internal class FFmpegMediaConverter : IMediaConverter
    {
        readonly string DefaultOutputDirPath = "output";
        FFmpegWrapper _ffmpeg;
        ILogger _logger;

        // TODO : add log handler instead of console
        public FFmpegMediaConverter(ILogger logger)
        {
            _logger = logger;
            _ffmpeg = new FFmpegWrapper(new FFmpegWrapper.Options() 
            { 
                BinaryFolder = AppDomain.CurrentDomain.BaseDirectory, 
                OutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultOutputDirPath),
                AutoDownloadBinaries = true
            }, logger);
        }

        public string OutputDir { get => _ffmpeg.OutputDir; set => _ffmpeg.OutputDir = value; }

        public IProgress DefaultProgressReport
        {
            get => _ffmpeg.DefaultProgressReport; 
            set => _ffmpeg.DefaultProgressReport = value; 
        }

        public async Task<string> ConvertVideoToMultipleMp3s(string videoPath, IEnumerable<IVideoSegment> segments, IProgress progressReport = null)
        {
            var videoName = Path.GetFileNameWithoutExtension(videoPath).RemoveInvalidChars();

            string outputDir = "";
            List<IVideoSegment> chapterList = segments.ToList();
            for (int i = 0; i < chapterList.Count; i++)
            {
                var chapter = chapterList[i];
                var fileName = Path.Combine(videoName, chapter.Title.RemoveInvalidChars() + ".mp3");

                var start = chapter.SegmentStartTimeMs;
                var end = i + 1 != chapterList.Count ? chapterList[i + 1].SegmentStartTimeMs : 0;
                var title = chapter.Title;

                var path = await _ffmpeg.VideoSegmentToMp3(videoPath, start, end, title, fileName, progressReport);
                if(string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(path);
            }

            return outputDir;
        }

        public async Task<string> ConvertVideoToMp3(string videoPath, string fileName = null, IProgress progressReport = null)
        {
            fileName ??= $"{Path.GetFileNameWithoutExtension(videoPath)}.mp3";
            return await _ffmpeg.ConvertToMp3(videoPath, fileName, progressReport);
        }

        public async Task<string> ConvertVideosToSingleMp3(IEnumerable<string> videoPaths, string fileName, IDictionary<string, IProgress> tempVideoProgresses = null)
        {
            var filename = $"{fileName}.mp3";
            return await _ffmpeg.VideosToSingleMp3(videoPaths, filename);
        }
    }
}