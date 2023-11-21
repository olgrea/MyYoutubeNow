namespace MyYoutubeNow.Progress
{
    public interface IVideoProgress
    {
        IProgress DownloadProgress { get; }
        IProgress ConvertProgress { get; }
    }
}
