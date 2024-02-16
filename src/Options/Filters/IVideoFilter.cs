using MyYoutubeNow.Client;

namespace MyYoutubeNow.Options.Filters
{
    public interface IVideoFilter
    {
        bool ShouldFilter(IVideoInfo video);
    }
}
