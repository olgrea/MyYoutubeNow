using System.Collections.Generic;
using System.Threading.Tasks;
using MyYoutubeNow.Progress;

namespace MyYoutubeNow.Converters
{
    public interface IVideoSegment
    {
        public string Title { get; }
        public ulong SegmentStartTimeMs { get; }
    }

    public interface IMediaConverter
    {
        public IProgress DefaultProgressReport { get; set; }
        public Task<bool> ConvertVideoToMp3(string videoPath, string outputDirPath, IProgress progressReport = null);
        public Task ConvertVideoToMultipleMp3s(string videoPath, IEnumerable<IVideoSegment> segments, string outputDirPath, IProgress progressReport = null);
        public Task<bool> ConvertVideosToSingleMp3(IEnumerable<string> pathsToMerge, string outputDirPath, string outputFileName, IDictionary<string, IProgress> tempVideoProgresses);
    }
}
