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
using MyYoutubeNow.Client;

namespace MyYoutubeNowApp.ViewModels;

public partial class MainViewModel : ObservableValidator
{
    readonly string DefaultOutputDirPath = AppDomain.CurrentDomain.BaseDirectory;
    private MyYoutubeNowService _myn;
    private IPlaylist? _playlist;
    private IVideo? _video;

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
        _video = null;
        _playlist = null;
        
        if (MyYoutubeNowService.IsVideo(Url))
        {
            _video = await _myn.GetVideoInfoAsync(Url);
            VideoList.Add(new VideoViewModel(_video) 
            {
                Selected = !File.Exists(Path.Combine(OutputDir, $"{_video.Title.RemoveInvalidChars()}.mp3"))
            });
        }
        else if(MyYoutubeNowService.IsPlaylist(Url)) 
        {
            _playlist = await _myn.GetPlaylistInfoAsync(Url);

            await foreach(IVideo vid in _myn.GetPlaylistVideosInfoAsync(Url))
            {
                VideoList.Add(new VideoViewModel(vid) 
                {
                    Selected = !File.Exists(Path.Combine(Path.Combine(OutputDir, _playlist.Title.RemoveInvalidChars()), $"{vid.Title.RemoveInvalidChars()}.mp3"))
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
        if(_video != null)
        {
            _myn.OutputDir = OutputDir;
            await _myn.ConvertVideo(_video);
        }
        else if(_playlist != null)
        {
            _myn.OutputDir = OutputDir;
            var filters = VideoList.Where(v => !v.Selected).Select(v => new VideoIdFilter(v.Id));
            await _myn.ConvertPlaylist(_playlist, filters);
        }
        else
        {
            throw new ArgumentException("No playlist or video");
        }
    }
}
