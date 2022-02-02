using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyYoutubeNow.Client;
using MyYoutubeNow.Utils;

namespace MyYoutubeNow.Converters
{
    public class MediaConverter
    {
        FFmpegWrapper _ffmpeg;
        string _baseDirectory;

        // TODO : add log handler instead of console
        public MediaConverter()
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _ffmpeg = new FFmpegWrapper(_baseDirectory);
        }

        public async Task<string> ConvertToMp3(IEnumerable<string> pathsToConvert, string outputDirName = "output")
        {
            var list = pathsToConvert.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                Console.WriteLine($"Conversion {i+1}/{list.Count}");
                await ConvertToMp3(list[i], outputDirName);
            }

            return Path.Combine(Directory.GetCurrentDirectory(), outputDirName);
        }

        public async Task<string> ConvertToMp3s(string videoMixPath, IEnumerable<Chapter> chapters, string outputDirName = "output")
        {
            var tempDir = videoMixPath.Replace(Path.GetFileName(videoMixPath), "");

            var outputDir = Path.Combine(_baseDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);
            
            var chapterList = chapters.ToList();
            for (int i = 0; i < chapterList.Count; i++)
            {
                var chapter = chapterList[i];
                var partPath = Path.Combine(_baseDirectory, outputDirName, chapter.Title.RemoveInvalidChars() + ".mp3");

                var start = chapter.TimeRangeStartMs;
                var end = i + 1 != chapterList.Count ? chapterList[i + 1].TimeRangeStartMs : 0;
                var title = chapter.Title;

                await _ffmpeg.VideoPartToMp3(videoMixPath, partPath, start, end, title);
            }

            Directory.Delete(tempDir, true);
            return outputDirName;
        }

        public async Task<bool> ConvertToMp3(string pathToConvert, string outputDirName = "output")
        {
            var outputDir = Path.Combine(_baseDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);

            var filename = $"{Path.GetFileNameWithoutExtension(pathToConvert)}.mp3";
            var filePath = Path.Combine(outputDir, filename);
            return await _ffmpeg.ConvertToMp3(pathToConvert, filePath);
        }

        public async Task<bool> ConcatenateMp3s(IEnumerable<string> pathsToMerge, string outputFileName, string outputDirName = "output")
        {
            var outputDir = Path.Combine(_baseDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);

            var filename = $"{outputFileName}.mp3";
            var filePath = Path.Combine(outputDir, filename);

            return await _ffmpeg.VideosToSingleMp3(pathsToMerge, filePath);
        }
    }
}