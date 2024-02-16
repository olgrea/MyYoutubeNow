using System.Collections.Generic;
using MyYoutubeNow.Options.Filters;
using System.Linq;

namespace MyYoutubeNow.Options
{
    public class PlaylistOptions : IPlaylistOptions
    {
        public IEnumerable<IVideoFilter> Filters { get; set; } = Enumerable.Empty<IVideoFilter>();
        public IEnumerable<IVideoOptions> VideoConversionOptions { get; set; } = Enumerable.Empty<IVideoOptions>();
        public bool Concatenate { get; set; } = false;
    }
}
