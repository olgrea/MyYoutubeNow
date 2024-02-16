using System.Collections.Generic;
using MyYoutubeNow.Client;

namespace MyYoutubeNow.Progress
{
    public interface IPlaylistProgress
    {
        IDictionary<IPlaylistVideoInfo, IVideoProgress> VideoProgresses { get; }
    }

    internal class PlaylistProgress : IPlaylistProgress
    {
        public IDictionary<IPlaylistVideoInfo, IVideoProgress> VideoProgresses { get; } = new Dictionary<IPlaylistVideoInfo, IVideoProgress>();
    }
}
