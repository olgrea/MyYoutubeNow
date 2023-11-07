using YoutubeExplode.Videos;

namespace MyYoutubeNow.Client
{
    public class VideoIdFilter : IPlaylistVideoFilter
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
