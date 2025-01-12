using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyYoutubeNow.Converters;
using MyYoutubeNow.Options.Filters;
using MyYoutubeNow.Progress;

namespace MyYoutubeNow.Client
{
    public interface IVideoInfo
    {
        public string Id { get; }
        public string Url => $"https://www.youtube.com/watch?v={Id}";
        public string Title { get; }
        public TimeSpan? Duration { get; }
        public string ThumbnailUrl { get; }
    }

    public interface IPlaylistVideoInfo : IVideoInfo
    {
        public string PlaylistId { get; }
    }

    public interface IPlaylistInfo
    {
        public string Id { get; }
        public string Url => $"https://www.youtube.com/playlist?list={Id}";
        public string Title { get; }
    }

    public record DownloadedVideo(IPlaylistVideoInfo VideoInfo, string Path);

    public class Chapter
    {
        public Chapter(string title, ulong segmentStartTimeMs)
        {
            Title = title;
            SegmentStartTimeMs = segmentStartTimeMs;
        }

        public string Title { get; set; }
        public ulong SegmentStartTimeMs { get; set; }
    }

    public interface IYoutubeClient
    {
        public IProgress DefaultProgressReport { get; set; }

        public bool IsVideo(string url);
        public bool IsPlaylist(string url);
        public Task<IVideoInfo> GetVideoInfoAsync(string url);
        public Task<IPlaylistInfo> GetPlaylistInfoAsync(string url);
        public IAsyncEnumerable<IPlaylistVideoInfo> GetPlaylistVideosInfoAsync(string url);
        public Task<string> DownloadVideo(IVideoInfo video, IProgress progress = null);
        public Task<IEnumerable<DownloadedVideo>> DownloadPlaylist(IPlaylistInfo info, IEnumerable<IVideoFilter> filters = null, IPlaylistProgress playlistProgress = null);
        public Task<IEnumerable<Chapter>> GetChaptersAsync(IVideoInfo video);
    }
}
