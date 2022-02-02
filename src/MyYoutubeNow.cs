using System.IO;
using System.Threading.Tasks;

using MyYoutubeNow.Converters;
using MyYoutubeNow.Client;
using MyYoutubeNow.Utils;

using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;

namespace MyYoutubeNow
{
    
    public class MyYoutubeNow
    {
        YoutubeClient _client;
        MediaConverter _converter;

        public MyYoutubeNow()
        {
            _client = new YoutubeClient();
            _converter = new MediaConverter();
        }

        static public bool IsVideo(string url) => VideoId.TryParse(url) != null;
        static public bool IsPlaylist(string url) => PlaylistId.TryParse(url) != null;

        public async Task ConvertVideo(string url, bool split = false)
        {
            VideoId id = VideoId.Parse(url);
            Video info = await _client.GetVideoInfoAsync(id);
            var videoPath = await _client.DownloadVideo(id, info);
            if (split)
            {
                var chapters = await _client.GetChaptersAsync(id);
                await _converter.ConvertToMp3s(videoPath, chapters, info.Title.RemoveInvalidChars());
            }
            else
            {
                await _converter.ConvertToMp3(videoPath);
            }
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }

        public async Task ConvertPlaylist(string url, bool concatenate = false)
        {
            PlaylistId id = PlaylistId.Parse(url);
            Playlist info = await _client.GetPlaylistInfoAsync(id);
            var videoPaths = await _client.DownloadPlaylist(id, info);

            if (concatenate)
            {
                await _converter.ConcatenateMp3s(videoPaths, $"{info.Title.RemoveInvalidChars()}");
            }
            else
            {
                await _converter.ConvertToMp3(videoPaths, $"{info.Title.RemoveInvalidChars()}");
            }
            foreach (var path in videoPaths)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
