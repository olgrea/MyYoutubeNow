using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyYoutubeNow;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace MyYoutubeNowApp.ViewModels;

public partial class MainViewModel : ObservableValidator
{
    // TODO : dependency injection
    private MyYoutubeNowService _myn;

    public MainViewModel(MyYoutubeNowService myn)
    {
        _myn = myn;
    }

    public string Url { get; set; } = string.Empty;
    
    public VideoListViewModel? Video { get; set; }

    [RelayCommand]
    private async Task GetUrlInfo()
    {
        if(MyYoutubeNowService.IsVideo(Url))
        {
            Video vid = await _myn.GetVideoInfoAsync(Url);
        }
        else if(MyYoutubeNowService.IsPlaylist(Url)) 
        {
            Playlist pl = await _myn.GetPlaylistInfoAsync(Url);
        }
        else
        {
            throw new ArgumentException("Unrecognized url");
        }
    }

}
