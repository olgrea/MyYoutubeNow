using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MyYoutubeNow.Progress;

namespace MyYoutubeNowApp
{
    public partial class ProgressViewModel : ObservableObject, IVideoProgress
    {
        class ActionProgressReport : IProgress
        {
            public ActionProgressReport(Action<double> onProgressUpdated)
            {
                ProgressUpdated = onProgressUpdated;
            }

            public Action<double>? ProgressUpdated { get; init; }
            public void Report(double value) => ProgressUpdated?.Invoke(value);
        }

        private double _downloadProgress;
        private double _conversionProgress;
        Action _onComplete;

        public ProgressViewModel(Action onComplete)
        {
            DownloadProgress = new ActionProgressReport(OnDownLoadProgressUpdated);
            ConvertProgress = new ActionProgressReport(OnConversionProgressUpdated);
            _onComplete = onComplete;
        }

        [ObservableProperty]
        private int _totalProgress;

        public IProgress DownloadProgress { get; init; }

        public IProgress ConvertProgress { get; init; }

        void OnDownLoadProgressUpdated(double value)
        {
            _downloadProgress = value;
            ComputeTotalProgress();
        }

        void OnConversionProgressUpdated(double value)
        {
            _conversionProgress = value;
            ComputeTotalProgress();
        }

        void ComputeTotalProgress()
        {
            // Post on main sync context?
            TotalProgress = Convert.ToInt32(100*(_downloadProgress + _conversionProgress)/2.0);
            if (TotalProgress >= 100)
                _onComplete?.Invoke();
        }
    }
}
