using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using MyYoutubeNow.Utils;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace MyYoutubeNow
{
    public class Options
    {
        [Value(0, Required = true, HelpText = "The url of the video/playlist. ")]
        public string Url { get; set; }

        [Option('s', Default = false, HelpText = "Split a video with chapters and convert them into mp3s.")]
        public bool Split { get; set; }

        [Option('c', Hidden = true, Default = false, HelpText = "Concatenate videos of a playlist into a single mp3.")]
        public bool Concatenate { get; set; }
    }

    public class CLI
    {
        static async Task Main(string[] args)
        {
            var results = CommandLine.Parser.Default.ParseArguments<Options>(args);
            if (results.Errors.Any())
            {
                foreach (Error error in results.Errors) 
                    Console.WriteLine($"{error} - {error.Tag}");
                return;
            }

            await results.WithParsedAsync(CLI.Execute);
        }
        
        public static async Task Execute(Options options)
        {
            var client = new YoutubeClient();
            var converter = new Converter(); 
            
            if (VideoId.TryParse(options.Url) != null)
            {
                VideoId id = new VideoId(options.Url);
                Video info = await client.GetVideoInfoAsync(id);
                var videoPath = await client.DownloadVideo(id, info);
                if (options.Split)
                {
                    var chapters = await client.GetChaptersAsync(id);
                    await converter.ConvertToMp3s(videoPath, chapters, info.Title.RemoveInvalidChars());
                }
                else
                {
                    await converter.ConvertToMp3(videoPath);
                }
                if (File.Exists(videoPath)) 
                    File.Delete(videoPath);
            }
            else if(PlaylistId.TryParse(options.Url) != null)
            {
                PlaylistId id = new PlaylistId(options.Url);
                Playlist info = await client.GetPlaylistInfoAsync(id);
                var videoPaths = await client.DownloadPlaylist(id, info);

                if (options.Concatenate)
                {
                    await converter.ConcatenateMp3s(videoPaths);
                }
                else
                {
                    await converter.ConvertToMp3(videoPaths, $"{info.Title.RemoveInvalidChars()}");
                }
                foreach (var path in videoPaths)
                {
                    if (File.Exists(path)) 
                        File.Delete(path);
                }
            }
        }
    }
}
