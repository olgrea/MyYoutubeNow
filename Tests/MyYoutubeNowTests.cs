using NUnit.Framework;
using MyYoutubeNow;
using System.Threading.Tasks;
using System;
using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;
using YoutubeExplode.Exceptions;

namespace Tests
{
    public class MyYoutubeNowTests
    {
        MyYoutubeNowService _myns;

        [SetUp] 
        public void SetUp() 
        {
            _myns = new MyYoutubeNowService();
        }

        [Test]
        public async Task GetVideoInfoAsync_ValidUrl_RetrievesIt()
        {
            string id = "atBi_MfT3LE";
            string url = $"https://www.youtube.com/watch?v={id}";
            IVideo vid = await _myns.GetVideoInfoAsync(url);

            Assert.That(vid, Is.Not.Null);
            Assert.That(vid.Id.ToString(), Is.EqualTo(id));
        }

        [Test]
        public void GetVideoInfoAsync_InvalidUrl_Throws()
        {
            string invalidUrl1 = "https://www.youtube.com/watch?v=ssssssssss";
            string invalidUrl2 = "dqasdfawq";
            Assert.ThrowsAsync<PlaylistUnavailableException>(() =>  _myns.GetVideoInfoAsync(invalidUrl1));
            Assert.ThrowsAsync<PlaylistUnavailableException>(() =>  _myns.GetVideoInfoAsync(invalidUrl2));
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
            string invalidUrl = "https://www.youtube.com/playlist?list=PL1qgThHfu0PaX44brExT3vTdwCQAm334s";
            Assert.ThrowsAsync<PlaylistUnavailableException>(() => _myns.GetPlaylistInfoAsync(invalidUrl));
        }

        [Test]
        public async Task GetPlaylistInfoAsync_ValidUrl_UnlistedPlaylist_RetrievesIt()
        {
            string id = "PL1qgThHfu0Pbsd7VgxBJWTWI0pAD0basw";
            string url = $"https://www.youtube.com/playlist?list={id}";
            IPlaylist pl = await _myns.GetPlaylistInfoAsync(url);

            Assert.That(pl, Is.Not.Null);
            Assert.That(pl.Id.ToString(), Is.EqualTo(id));
        }

        [Test]
        public async Task GetPlaylistInfoAsync_ValidUrl_PublicPlaylist_RetrievesIt()
        {
            string id = "PL1qgThHfu0PZ2lXXOJLLkYaCvxw6RUsPw";
            string url = $"https://www.youtube.com/playlist?list={id}";
            IPlaylist pl = await _myns.GetPlaylistInfoAsync(url);

            Assert.That(pl, Is.Not.Null);
            Assert.That(pl.Id.ToString(), Is.EqualTo(id));
        }
    }
}