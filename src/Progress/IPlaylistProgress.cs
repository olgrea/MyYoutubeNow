using System.Collections.Generic;
using YoutubeExplode.Videos;

namespace MyYoutubeNow.Progress
{
    public interface IPlaylistProgress
    {
        IDictionary<VideoId, IVideoProgress> VideoProgresses { get; }
    }

    internal class PlaylistProgress : IPlaylistProgress
    {
        public IDictionary<VideoId, IVideoProgress> VideoProgresses { get; } = new Dictionary<VideoId, IVideoProgress>();
    }
}
