using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyYoutubeNow;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Common;
using System.IO;
using MyYoutubeNow.Utils;
using System.Linq;
using MyYoutubeNow.Options.Filters;
using MyYoutubeNow.Options;
using MyYoutubeNow.Progress;

namespace MyYoutubeNowApp.ViewModels;

public partial class MainViewModel : ObservableValidator
{
    readonly string DefaultOutputDirPath = AppDomain.CurrentDomain.BaseDirectory;
    private MyYoutubeNowService _myn;
    private UrlInfo? _urlInfo = null;

    class PlaylistProgress : IPlaylistProgress
    {
        public PlaylistProgress(IDictionary<VideoId, IVideoProgress> videoProgresses)
        {
            VideoProgresses = videoProgresses;
        }

        public IDictionary<VideoId, IVideoProgress> VideoProgresses { get; } = new Dictionary<VideoId, IVideoProgress>();
    }

#if DEBUG
    // IDE wants a parameterless constructor to display design preview
    public MainViewModel() { }
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
        
        if (MyYoutubeNowService.IsVideo(Url))
        {
            IVideo video = await _myn.GetVideoInfoAsync(Url);
            _urlInfo = new VideoUrlInfo(video);

            bool exists = VideoExists(video);
            VideoList.Add(new VideoViewModel(video)
            {
                Selected = !exists
            });
        }
        else if(MyYoutubeNowService.IsPlaylist(Url)) 
        {
            IPlaylist playlist = await _myn.GetPlaylistInfoAsync(Url);
            _urlInfo = new PlaylistUrlInfo(playlist);

            await foreach(IVideo vid in _myn.GetPlaylistVideosInfoAsync(Url))
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
            var options = new PlaylistOptions() { Filters = VideoList.Where(v => !v.Selected).Select(v => new VideoIdFilter(v.Id)) };

            IPlaylistProgress plProg = new PlaylistProgress(VideoList.ToDictionary(v => v.Id, v => (IVideoProgress)v.Progress));

            await _myn.ConvertPlaylist(pInfo.Playlist, options, plProg);
        }
        else
        {
            throw new ArgumentException("No playlist or video");
        }
    }

    private bool VideoExists(IVideo video)
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
