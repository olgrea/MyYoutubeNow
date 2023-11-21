using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MyYoutubeNow.Utils;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

namespace MyYoutubeNowApp.ViewModels
{
    public partial class VideoViewModel : ObservableObject
    {
        IVideo _video;

        public VideoViewModel(IVideo video)
        {
            _video = video;          
            _title = video.Title;
            _url = video.Url;
            _thumbnail = video.Thumbnails.FirstOrDefault();
            _duration = video.Duration;
        }

        public VideoId Id => _video.Id;

        [ObservableProperty]
        private string _url;

        [ObservableProperty]
        private bool _selected;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private TimeSpan? _duration;

        [ObservableProperty]
        private Thumbnail? _thumbnail;
    }
}
