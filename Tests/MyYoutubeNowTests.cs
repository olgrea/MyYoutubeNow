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

            //TODO : add a way to specify output folder
            string mp3FilePath = Path.Combine("output", info.Title.RemoveInvalidChars() + ".mp3");
            Assert.That(File.Exists(mp3FilePath));
        }

        [Test]
        public async Task ConvertPlaylist_ValidUrl_DownloadsAndConvertsAllVideosInIt()
        {
            string url = string.Format(PlaylistUrlFormat, PublicPlaylistId);

            // TODO : use GetPlaylistVideosInfoAsync() after merging in avalonia app branch
            IPlaylist info = await _myns.GetPlaylistInfoAsync(url);

            await _myns.ConvertPlaylist(url);

            string mp3DirPath = info.Title.RemoveInvalidChars();
            Assert.That(Directory.Exists(mp3DirPath));
        }

        [Test]
        public async Task ConvertPlaylist_ValidUrl_Concatenate_DownloadsAndConvertsAllVideosInItToASingleFile()
        {
            string url = string.Format(PlaylistUrlFormat, PublicPlaylistId);

            // TODO : use GetPlaylistVideosInfoAsync() after merging in avalonia app branch
            IPlaylist info = await _myns.GetPlaylistInfoAsync(url);

            await _myns.ConvertPlaylist(url, concatenate: true);

            string mp3FilePath = Path.Combine("output", info.Title.RemoveInvalidChars() + ".mp3");
            Assert.That(File.Exists(mp3FilePath));
        }
    }
}