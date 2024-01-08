using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyYoutubeNow.Client;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Utils;
using NLog;

namespace MyYoutubeNow.Converters
{
    public class MediaConverter
    {
        readonly string DefaultOutputDirPath = AppDomain.CurrentDomain.BaseDirectory;
        FFmpegWrapper _ffmpeg;
        ILogger _logger;

        // TODO : add log handler instead of console
        public MediaConverter(ILogger logger)
        {
            _logger = logger;
            _ffmpeg = new FFmpegWrapper(DefaultOutputDirPath, logger);
        }
      
        public IProgress DefaultProgressReport 
        {
            get => _ffmpeg.DefaultProgressReport; 
            set => _ffmpeg.DefaultProgressReport = value; 
        }

        internal async Task ConvertToMp3(IEnumerable<TempVideo> pathsToConvert, string outputDirPath, IDictionary<TempVideo, IProgress> tempVideoProgresses = null)
        {
            var list = pathsToConvert.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                _logger.Info($"Conversion {i+1}/{list.Count}");

                IProgress progress = null;
                tempVideoProgresses?.TryGetValue(list[i], out progress);

                await ConvertToMp3(list[i].Path, outputDirPath, progress);
            }
        }

        public async Task ConvertToMp3s(string videoMixPath, IEnumerable<Chapter> chapters, string outputDirPath, IProgress progressReport = null)
        {
            if (!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var chapterList = chapters.ToList();
            for (int i = 0; i < chapterList.Count; i++)
            {
                var chapter = chapterList[i];
                var partPath = Path.Combine(outputDirPath, chapter.Title.RemoveInvalidChars() + ".mp3");

                var start = chapter.TimeRangeStartMs;
                var end = i + 1 != chapterList.Count ? chapterList[i + 1].TimeRangeStartMs : 0;
                var title = chapter.Title;

                await _ffmpeg.VideoPartToMp3(videoMixPath, partPath, start, end, title, progressReport);
            }
        }

        internal async Task<bool> ConvertToMp3(string pathToConvert, string outputDirPath, IProgress progressReport = null)
        {
            if(!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var filename = $"{Path.GetFileNameWithoutExtension(pathToConvert)}.mp3";
            var filePath = Path.Combine(outputDirPath, filename);
            return await _ffmpeg.ConvertToMp3(pathToConvert, filePath, progressReport);
        }

        internal async Task<bool> ConcatenateMp3s(IEnumerable<TempVideo> pathsToMerge, string outputDirPath, string outputFileName, IDictionary<TempVideo, IProgress> tempVideoProgresses)
        {
            if (!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var filename = $"{outputFileName}.mp3";
            var filePath = Path.Combine(outputDirPath, filename);

            return await _ffmpeg.VideosToSingleMp3(pathsToMerge.Select(p => p.Path), filePath);
        }
    }
}