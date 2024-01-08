using NUnit.Framework;
using MyYoutubeNow;
using System.Threading.Tasks;
using System;
using YoutubeExplode.Videos;

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
            Video vid = await _myns.GetVideoInfoAsync(url);

            Assert.That(vid, Is.Not.Null);
            Assert.That(vid.Id.ToString(), Is.EqualTo(id));
        }

        [Test]
        public void GetVideoInfoAsync_InvalidUrl_Throws()
        {
            string invalidUrl1 = "https://www.youtube.com/watch?v=ssssssssss";
            string invalidUrl2 = "dqasdfawq";
            Assert.ThrowsAsync<ArgumentException>(() =>  _myns.GetVideoInfoAsync(invalidUrl1));
            Assert.ThrowsAsync<ArgumentException>(() =>  _myns.GetVideoInfoAsync(invalidUrl2));
        }
    }
}