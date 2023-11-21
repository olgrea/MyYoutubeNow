using System.Collections.Generic;
using MyYoutubeNow.Options.Filters;

namespace MyYoutubeNow.Options
{
    public interface IPlaylistOptions
    {
        public IEnumerable<IVideoFilter> Filters { get; }
        public IEnumerable<IVideoOptions> VideoConversionOptions { get; }
        public bool Concatenate { get; }
    }
}
