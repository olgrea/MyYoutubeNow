using YoutubeExplode.Videos;

namespace MyYoutubeNow.Client
{
    public interface IPlaylistVideoFilter
    {
        bool ShouldFilter(IVideo video);
    }
}
