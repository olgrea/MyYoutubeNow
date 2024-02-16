using MyYoutubeNow.Client;

namespace MyYoutubeNowApp.ViewModels
{
    internal interface UrlInfo { }

    internal class VideoUrlInfo : UrlInfo
    {
        public VideoUrlInfo(IVideoInfo video)
        {
            Video = video;
        }

        public IVideoInfo Video { get; init; }
    }

    internal class PlaylistUrlInfo : UrlInfo
    {
        public PlaylistUrlInfo(IPlaylistInfo playlist)
        {
            Playlist = playlist;
        }

        public IPlaylistInfo Playlist { get; init; }
    }
}
