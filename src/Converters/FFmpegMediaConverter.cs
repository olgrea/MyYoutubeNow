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
        readonly string DefaultOutputDirPath = AppDomain.CurrentDomain.BaseDirectory;
        FFmpegWrapper _ffmpeg;
        ILogger _logger;

        // TODO : add log handler instead of console
        public FFmpegMediaConverter(ILogger logger)
        {
            _logger = logger;
            _ffmpeg = new FFmpegWrapper(DefaultOutputDirPath, logger);
        }
      
        public IProgress DefaultProgressReport
        {
            get => _ffmpeg.DefaultProgressReport; 
            set => _ffmpeg.DefaultProgressReport = value; 
        }

        public async Task ConvertVideoToMultipleMp3s(string videoPath, IEnumerable<IVideoSegment> segments, string outputDirPath, IProgress progressReport = null)
        {
            if (!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var chapterList = segments.ToList();
            for (int i = 0; i < chapterList.Count; i++)
            {
                var chapter = chapterList[i];
                var partPath = Path.Combine(outputDirPath, chapter.Title.RemoveInvalidChars() + ".mp3");

                var start = chapter.SegmentStartTimeMs;
                var end = i + 1 != chapterList.Count ? chapterList[i + 1].SegmentStartTimeMs : 0;
                var title = chapter.Title;

                await _ffmpeg.VideoPartToMp3(videoPath, partPath, start, end, title, progressReport);
            }
        }

        public async Task<bool> ConvertVideoToMp3(string pathToConvert, string outputDirPath, IProgress progressReport = null)
        {
            if(!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var filename = $"{Path.GetFileNameWithoutExtension(pathToConvert)}.mp3";
            var filePath = Path.Combine(outputDirPath, filename);
            return await _ffmpeg.ConvertToMp3(pathToConvert, filePath, progressReport);
        }

        public async Task<bool> ConvertVideosToSingleMp3(IEnumerable<string> pathsToMerge, string outputDirPath, string outputFileName, IDictionary<string, IProgress> tempVideoProgresses)
        {
            if (!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var filename = $"{outputFileName}.mp3";
            var filePath = Path.Combine(outputDirPath, filename);

            return await _ffmpeg.VideosToSingleMp3(pathsToMerge, filePath);
        }
    }
}