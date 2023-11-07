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

namespace MyYoutubeNowApp.ViewModels;

public partial class MainViewModel : ObservableValidator
{
    private MyYoutubeNowService _myn;

#if DEBUG
    // IDE wants a parameterless constructor to display design preview
    public MainViewModel() { }
#endif

    public MainViewModel(MyYoutubeNowService myn)
    {
        _myn = myn;
    }

    public string Url { get; set; } = string.Empty;

    public ObservableCollection<VideoViewModel> VideoList { get; set; } = new ObservableCollection<VideoViewModel>();
    //public ObservableCollection<VideoViewModel> VideoList { get; set; } = BuildTestViewModels();

    [RelayCommand]
    private async Task PullUrlInfo()
    {
        VideoList.Clear();
        if(MyYoutubeNowService.IsVideo(Url))
        {
            IVideo vid = await _myn.GetVideoInfoAsync(Url);
            VideoList.Add(new VideoViewModel(vid));
        }
        else if(MyYoutubeNowService.IsPlaylist(Url)) 
        {
            IPlaylist pl = await _myn.GetPlaylistInfoAsync(Url);
            await foreach(IVideo vid in _myn.GetPlaylistVideosInfoAsync(Url))
            {
                VideoList.Add(new VideoViewModel(vid));
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
        await Task.CompletedTask;
    }

    static ObservableCollection<VideoViewModel> BuildTestViewModels()
    {
        return new ObservableCollection<VideoViewModel>()
        {
            new VideoViewModel()
            {
                Title = "Video1",
                Duration = TimeSpan.FromMinutes(1),
                Thumbnail = new Thumbnail(@"..\..\..\Assets\thumb1.png", new Resolution(64,64)),
            },
            new VideoViewModel()
            {
                Title = "Video2",
                Duration = TimeSpan.FromMinutes(2),
                Thumbnail = new Thumbnail(@"..\..\..\Assets\thumb2.png", new Resolution(64,64)),
            },
            new VideoViewModel()
            {
                Title = "Video3",
                Duration = TimeSpan.FromMinutes(3),
                Thumbnail = new Thumbnail(@"..\..\..\Assets\thumb3.png", new Resolution(64,64)),
            },
        };
    }
}
