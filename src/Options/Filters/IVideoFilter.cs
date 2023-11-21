using YoutubeExplode.Videos;

namespace MyYoutubeNow.Options.Filters
{
    public interface IVideoFilter
    {
        bool ShouldFilter(IVideo video);
    }
}
