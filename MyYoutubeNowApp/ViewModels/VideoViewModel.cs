using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MyYoutubeNow.Client;

namespace MyYoutubeNowApp.ViewModels
{
    public partial class VideoViewModel : ObservableObject
    {
        IVideoInfo _video;

        public VideoViewModel(IVideoInfo video)
        {
            _video = video;  
            _title = video.Title;
            _url = video.Url;
            _thumbnail = video.ThumbnailUrl;
            _duration = video.Duration;
            _progress = new(() => Downloaded = true);
        }

        internal IVideoInfo VideoInfo => _video;

        [ObservableProperty]
        private bool _selected;

        [ObservableProperty]
        private bool _downloaded;

        [ObservableProperty]
        private ProgressViewModel _progress;

        public string Id => _video.Id;

        [ObservableProperty]
        private string _url;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private TimeSpan? _duration;

        [ObservableProperty]
        private string? _thumbnail;
    }
}
