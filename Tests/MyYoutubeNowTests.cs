using NUnit.Framework;
using MyYoutubeNow;
using System.Threading.Tasks;
using System;
using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;
using YoutubeExplode.Exceptions;
using System.IO;
using MyYoutubeNow.Converters;
using MyYoutubeNow.Utils;
using MyYoutubeNow.Options;
using System.Collections.Generic;
using MyYoutubeNow.Progress;
using System.Linq;

namespace Tests
{
    public class MyYoutubeNowTests
    {
        const string VideoUrlFormat = "https://www.youtube.com/watch?v={0}";
        const string PlaylistUrlFormat = "https://www.youtube.com/playlist?list={0}";

        const string ValidVideoId = "atBi_MfT3LE";
        const string PrivatePlaylistId = "PL1qgThHfu0PYFdfBdyJyJFkILyk3Pkh9_";
        const string UnlistedPlaylistId = "PL1qgThHfu0Pbsd7VgxBJWTWI0pAD0basw";
        const string PublicPlaylistId = "PL1qgThHfu0PaX44brExT3vTdwCQAm334s";
        const string FFmpegExeName = FFmpegWrapper.FFmpegExeName;

        MyYoutubeNowService _myns;

        [SetUp] 
        public void SetUp() 
        {
            _myns = new MyYoutubeNowService();
        }

        [Test]
        public async Task GetVideoInfoAsync_ValidUrl_RetrievesIt()
        {
            string url = string.Format(VideoUrlFormat, ValidVideoId);
            IVideo vid = await _myns.GetVideoInfoAsync(url);

            Assert.That(vid, Is.Not.Null);
            Assert.That(vid.Id.ToString(), Is.EqualTo(ValidVideoId));
        }

        [Test]
        public void GetVideoInfoAsync_InvalidUrl_Throws()
        {
            string invalidUrl1 = "https://www.youtube.com/watch?v=ssssssssss";
            string invalidUrl2 = "dqasdfawq";
            Assert.ThrowsAsync<ArgumentException>(() =>  _myns.GetVideoInfoAsync(invalidUrl1));
            Assert.ThrowsAsync<ArgumentException>(() =>  _myns.GetVideoInfoAsync(invalidUrl2));
        }

        [Test]
        public void GetPlaylistInfoAsync_InvalidUrl_Throws()
        {
            string invalidUrl1 = "https://www.youtube.com/watch?v=atBi_MfT3LE&list=sssssssssssssssssssssss";
            string invalidUrl2 = "dqasdfawq";
            Assert.ThrowsAsync<PlaylistUnavailableException>(() => _myns.GetPlaylistInfoAsync(invalidUrl1));
            Assert.ThrowsAsync<PlaylistUnavailableException>(() => _myns.GetPlaylistInfoAsync(invalidUrl2));
        }

        [Test]
        public void GetPlaylistInfoAsync_ValidUrl_PrivatePlaylist_Throws()
        {
            string invalidUrl = string.Format(PlaylistUrlFormat, PrivatePlaylistId);
            Assert.ThrowsAsync<PlaylistUnavailableException>(() => _myns.GetPlaylistInfoAsync(invalidUrl));
        }

        [Test]
        public async Task GetPlaylistInfoAsync_ValidUrl_UnlistedPlaylist_RetrievesIt()
        {
            string url = string.Format(PlaylistUrlFormat, UnlistedPlaylistId);
            IPlaylist pl = await _myns.GetPlaylistInfoAsync(url);

            Assert.That(pl, Is.Not.Null);
            Assert.That(pl.Id.ToString(), Is.EqualTo(UnlistedPlaylistId));
        }

        [Test]
        public async Task GetPlaylistInfoAsync_ValidUrl_PublicPlaylist_RetrievesIt()
        {
            string url = string.Format(PlaylistUrlFormat, PublicPlaylistId);
            IPlaylist pl = await _myns.GetPlaylistInfoAsync(url);

            Assert.That(pl, Is.Not.Null);
            Assert.That(pl.Id.ToString(), Is.EqualTo(PublicPlaylistId));
        }

        [Test]
        public async Task ConvertVideo_FFmpegNotInstalled_DownloadsIt()
        {
            if (File.Exists(FFmpegExeName))
                File.Delete(FFmpegExeName);

            string url = string.Format(VideoUrlFormat, ValidVideoId);
            await _myns.ConvertVideo(url);

            Assert.That(File.Exists(FFmpegExeName));
        }

        [Test]
        public async Task ConvertVideo_ValidUrl_DownloadsAndConvertsIt()
        {
            string url = string.Format(VideoUrlFormat, ValidVideoId);
            IVideo info = await _myns.GetVideoInfoAsync(url);

            await _myns.ConvertVideo(url);

            string mp3FilePath = Path.Combine(_myns.OutputDir, info.Title.RemoveInvalidChars() + ".mp3");
            Assert.That(File.Exists(mp3FilePath));
        }

        [Test]
        public async Task ConvertVideo_ProgressIsUpdated()
        {
            string url = string.Format(VideoUrlFormat, ValidVideoId);

            VideoProgress progress = new VideoProgress();
            await _myns.ConvertVideo(url, progress);

            Assert.That(progress.Download, Is.EqualTo(1.0));
            Assert.That(progress.Conversion, Is.EqualTo(1.0));
        }

        [Test]
        public async Task ConvertPlaylist_ValidUrl_DownloadsAndConvertsAllVideosInIt()
        {
            string url = string.Format(PlaylistUrlFormat, PublicPlaylistId);

            string dirPath = (await _myns.GetPlaylistInfoAsync(url)).Title.RemoveInvalidChars();
            HashSet<string> fileNames = new();
            await foreach (PlaylistVideo vid in _myns.GetPlaylistVideosInfoAsync(url))
                fileNames.Add(vid.Title.RemoveInvalidChars() + ".mp3");

            await _myns.ConvertPlaylist(url);

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(dirPath));
                foreach(var file in Directory.EnumerateFiles(dirPath))
                    Assert.That(fileNames, Contains.Item(Path.GetFileName(file).RemoveInvalidChars()));
            });
        }

        [Test]
        public async Task ConvertPlaylist_ProgressIsUpdated()
        {
            string url = string.Format(PlaylistUrlFormat, PublicPlaylistId);

            string dirPath = (await _myns.GetPlaylistInfoAsync(url)).Title.RemoveInvalidChars();
            var playlistProgress = new PlaylistProgress();
            await foreach (PlaylistVideo vid in _myns.GetPlaylistVideosInfoAsync(url))
                playlistProgress.VideoProgresses.Add(vid.Id, new VideoProgress());

            await _myns.ConvertPlaylist(url, progresses);

            Assert.Multiple(() =>
            {
                foreach (VideoProgress progress in playlistProgress.VideoProgresses.Values.Cast<VideoProgress>())
                {
                    Assert.That(progress.Download, Is.EqualTo(1.0));
                    Assert.That(progress.Conversion, Is.EqualTo(1.0));
                }
            });
        }

        [Test]
        public async Task ConvertPlaylist_ValidUrl_Concatenate_DownloadsAndConvertsAllVideosInItToASingleFile()
        {
            string url = string.Format(PlaylistUrlFormat, PublicPlaylistId);

            IPlaylist info = await _myns.GetPlaylistInfoAsync(url);

            var opts = new PlaylistOptions() { Concatenate = true } ;
            await _myns.ConvertPlaylist(url, opts);

            string mp3FilePath = Path.Combine("output", info.Title.RemoveInvalidChars() + ".mp3");
            Assert.That(File.Exists(mp3FilePath));
        }

        class VideoProgress : IVideoProgress
        {
            class ValueProgress : IProgress
            {
                public double Value { get; set; }
                public void Report(double value) => Value = value;
            }

            ValueProgress _downloadProgress;
            ValueProgress _convertProgress;

            public VideoProgress()
            {
                _downloadProgress = new ValueProgress();
                _convertProgress = new ValueProgress();
            }

            public double Download => _downloadProgress.Value;
            public double Conversion => _convertProgress.Value;

            public IProgress DownloadProgress => _downloadProgress;
            public IProgress ConvertProgress => _convertProgress;
        }
    }
}