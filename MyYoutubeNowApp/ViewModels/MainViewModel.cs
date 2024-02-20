using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyYoutubeNow;
using MyYoutubeNow.Utils;
using MyYoutubeNow.Options.Filters;
using MyYoutubeNow.Options;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Client;

namespace MyYoutubeNowApp.ViewModels;

public partial class MainViewModel : ObservableValidator
{
    readonly string DefaultOutputDirPath = AppDomain.CurrentDomain.BaseDirectory;
    private MyYoutubeNowService _myn;
    private UrlInfo? _urlInfo = null;

    class PlaylistProgress : IPlaylistProgress
    {
        public PlaylistProgress(IDictionary<IPlaylistVideoInfo, IVideoProgress> videoProgresses)
        {
            VideoProgresses = videoProgresses;
        }

        public IDictionary<IPlaylistVideoInfo, IVideoProgress> VideoProgresses { get; } = new Dictionary<IPlaylistVideoInfo, IVideoProgress>();
    }

#if DEBUG
    // IDE wants a parameterless constructor to display design preview
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public MainViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#endif

    public MainViewModel(MyYoutubeNowService myn)
    {
        _myn = myn;
        OutputDir = DefaultOutputDirPath;
    }

    [ObservableProperty]
    private string _outputDir;

    public string Url { get; set; } = string.Empty;

    public ObservableCollection<VideoViewModel> VideoList { get; set; } = new ObservableCollection<VideoViewModel>();

    [RelayCommand]
    private async Task PullUrlInfo()
    {
        VideoList.Clear();
        
        if (_myn.IsVideo(Url))
        {
            IVideoInfo video = await _myn.GetVideoInfoAsync(Url);
            _urlInfo = new VideoUrlInfo(video);

            bool exists = VideoExists(video);
            VideoList.Add(new VideoViewModel(video)
            {
                Selected = !exists
            });
        }
        else if(_myn.IsPlaylist(Url)) 
        {
            IPlaylistInfo playlist = await _myn.GetPlaylistInfoAsync(Url);
            _urlInfo = new PlaylistUrlInfo(playlist);

            await foreach(IPlaylistVideoInfo vid in _myn.GetPlaylistVideosInfoAsync(Url))
            {
                bool exists = VideoExists(vid);
                VideoList.Add(new VideoViewModel(vid) 
                {
                    Selected = !exists,
                });
            }
        }
        else
        {
            throw new ArgumentException("Unrecognized url");
        }
    }

    [RelayCommand]
    public async Task Download()
    {
        if (_urlInfo is VideoUrlInfo vInfo)
        {
            _myn.OutputDir = OutputDir;

            await _myn.DownloadAndConvertVideo(vInfo.Video, VideoList.First().Progress);
        }
        else if (_urlInfo is PlaylistUrlInfo pInfo)
        {
            _myn.OutputDir = OutputDir;
            var options = new PlaylistOptions() { Filters = VideoList.Where(v => !v.Selected).Select(v => new VideoIdFilter(v.VideoInfo)) };

            IPlaylistProgress plProg = new PlaylistProgress(VideoList.ToDictionary(v => (IPlaylistVideoInfo)v.VideoInfo, v => (IVideoProgress)v.Progress));

            await _myn.ConvertPlaylist(pInfo.Playlist, options, plProg);
        }
        else
        {
            throw new ArgumentException("No playlist or video");
        }
    }

    private bool VideoExists(IVideoInfo video)
    {
        if (_urlInfo is VideoUrlInfo)
        {
            return File.Exists(Path.Combine(OutputDir, $"{video.Title.RemoveInvalidChars()}.mp3"));
        }
        else if (_urlInfo is PlaylistUrlInfo pInfo)
        {
            return File.Exists(Path.Combine(Path.Combine(OutputDir, pInfo.Playlist.Title.RemoveInvalidChars()), $"{video.Title.RemoveInvalidChars()}.mp3"));
        }

        return false;
    }
}
