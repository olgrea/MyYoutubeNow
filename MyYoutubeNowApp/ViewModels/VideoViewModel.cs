using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

namespace MyYoutubeNowApp.ViewModels
{
    public partial class VideoViewModel : ObservableObject
    {
        IVideo _video;

        public VideoViewModel() { }

        public VideoViewModel(IVideo video)
        {
            _video = video;

            _title = video.Title;
            _thumbnail = video.Thumbnails.FirstOrDefault();
            _duration = video.Duration;
        }

        [ObservableProperty]
        public string _title;

        [ObservableProperty]
        public TimeSpan? _duration;

        [ObservableProperty]
        public Thumbnail? _thumbnail;
    }
}
