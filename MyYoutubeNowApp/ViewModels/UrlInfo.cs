using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace MyYoutubeNowApp.ViewModels
{
    internal interface UrlInfo { }

    internal class VideoUrlInfo : UrlInfo
    {
        public VideoUrlInfo(IVideo video)
        {
            Video = video;
        }

        public IVideo Video { get; init; }
    }

    internal class PlaylistUrlInfo : UrlInfo
    {
        public PlaylistUrlInfo(IPlaylist playlist)
        {
            Playlist = playlist;
        }

        public IPlaylist Playlist { get; init; }
    }
}
