using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

namespace MyYoutubeNowApp.ViewModels
{
    public partial class VideoViewModel : ObservableObject
    {
        IVideo _video;
        string _outputDir;

        public VideoViewModel() { }

        public VideoViewModel(IVideo video, string outputDir)
        {
            _video = video;
            
            _outputDir = outputDir;

            _title = video.Title;
            _thumbnail = video.Thumbnails.FirstOrDefault();
            _duration = video.Duration;
        }

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private TimeSpan? _duration;

        [ObservableProperty]
        private Thumbnail? _thumbnail;
    }
}
