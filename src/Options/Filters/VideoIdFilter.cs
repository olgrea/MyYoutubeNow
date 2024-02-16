using MyYoutubeNow.Client;

namespace MyYoutubeNow.Options.Filters
{
    public class VideoIdFilter : IVideoFilter
    {
        public VideoIdFilter(IVideoInfo videoId)
        {
            VideoInfo = videoId;
        }

        public IVideoInfo VideoInfo { get; init; }

        public bool ShouldFilter(IVideoInfo video)
        {
            return VideoInfo.Id == video.Id;
        }
    }
}
