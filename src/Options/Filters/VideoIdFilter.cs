using YoutubeExplode.Videos;

namespace MyYoutubeNow.Options.Filters
{
    public class VideoIdFilter : IVideoFilter
    {
        public VideoIdFilter(VideoId videoId)
        {
            VideoId = videoId;
        }

        public VideoId VideoId { get; init; }

        public bool ShouldFilter(IVideo video)
        {
            return VideoId == video.Id;
        }
    }
}
