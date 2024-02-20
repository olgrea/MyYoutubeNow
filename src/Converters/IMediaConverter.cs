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
        public string OutputDir { get; set; }
        public IProgress DefaultProgressReport { get; set; }
        public Task<bool> ConvertVideoToMp3(string videoPath, string fileName = null, IProgress progressReport = null);
        public Task ConvertVideoToMultipleMp3s(string videoPath, IEnumerable<IVideoSegment> segments, IProgress progressReport = null);
        public Task<bool> ConvertVideosToSingleMp3(IEnumerable<string> pathsToMerge, string fileName, IDictionary<string, IProgress> tempVideoProgresses = null);
    }
}
