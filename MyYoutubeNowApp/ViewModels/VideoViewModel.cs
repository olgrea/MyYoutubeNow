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
        string _outputDir;

        public VideoViewModel(IVideo video, string outputDir)
        {
            _video = video;
            
            _outputDir = outputDir;
            _exists = File.Exists(Path.Combine(outputDir, $"{video.Title.RemoveInvalidChars()}.mp3"));

            _title = video.Title;
            _thumbnail = video.Thumbnails.FirstOrDefault();
            _duration = video.Duration;
        }

        public VideoId Id => _video.Id;

        [ObservableProperty]
        public bool _exists;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private TimeSpan? _duration;

        [ObservableProperty]
        private Thumbnail? _thumbnail;
    }
}
