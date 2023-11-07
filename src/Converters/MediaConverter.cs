using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyYoutubeNow.Client;
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
      
        public IProgressReport ProgressReport 
        {
            get => _ffmpeg.ProgressReport; 
            set => _ffmpeg.ProgressReport = value; 
        }

        public async Task ConvertToMp3(IEnumerable<string> pathsToConvert, string outputDirPath)
        {
            var list = pathsToConvert.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                _logger.Info($"Conversion {i+1}/{list.Count}");
                await ConvertToMp3(list[i], outputDirPath);
            }
        }

        public async Task ConvertToMp3s(string videoMixPath, IEnumerable<Chapter> chapters, string outputDirPath)
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

                await _ffmpeg.VideoPartToMp3(videoMixPath, partPath, start, end, title);
            }
        }

        public async Task<bool> ConvertToMp3(string pathToConvert, string outputDirPath)
        {
            if(!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var filename = $"{Path.GetFileNameWithoutExtension(pathToConvert)}.mp3";
            var filePath = Path.Combine(outputDirPath, filename);
            return await _ffmpeg.ConvertToMp3(pathToConvert, filePath);
        }

        public async Task<bool> ConcatenateMp3s(IEnumerable<string> pathsToMerge, string outputDirPath, string outputFileName)
        {
            if (!Directory.Exists(outputDirPath))
                Directory.CreateDirectory(outputDirPath);

            var filename = $"{outputFileName}.mp3";
            var filePath = Path.Combine(outputDirPath, filename);

            return await _ffmpeg.VideosToSingleMp3(pathsToMerge, filePath);
        }
    }
}