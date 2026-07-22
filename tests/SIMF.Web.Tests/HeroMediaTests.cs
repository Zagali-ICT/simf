// D-756 — unit tests for HeroMedia.Classify (SIMF.Web): it turns the CP-editable
// OrganizationProfile.BackgroundVideoUrl into a render decision — a YouTube link
// becomes an embed video id (iframe), a direct MP4/HLS link becomes a file URL
// (<video>), and anything else becomes null so the hero falls back to the bundled
// asset. Pure + static, so no HTTP stub is needed.
using SIMF.Web.Content;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class HeroMediaTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=rmW5sJTp-Zo", "rmW5sJTp-Zo")]
    [InlineData("https://youtu.be/rmW5sJTp-Zo", "rmW5sJTp-Zo")]
    [InlineData("https://www.youtube.com/embed/rmW5sJTp-Zo", "rmW5sJTp-Zo")]
    public void A_youtube_link_classifies_as_an_embed_id(string url, string expectedId)
    {
        var source = HeroMedia.Classify(url);

        Assert.NotNull(source);
        Assert.Equal(expectedId, source!.YouTubeId);
        Assert.Null(source.FileUrl);
    }

    [Theory]
    [InlineData("https://cdn.simf.test/hero.mp4")]
    [InlineData("https://cdn.simf.test/live/stream.m3u8")]
    public void A_direct_stream_classifies_as_a_file_url(string url)
    {
        var source = HeroMedia.Classify(url);

        Assert.NotNull(source);
        Assert.Null(source!.YouTubeId);
        Assert.Equal(url, source.FileUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://cdn.simf.test/hero.mp4")] // cleartext http is rejected
    [InlineData("https://www.youtube.com/@simfchannel")] // a channel link has no video id
    [InlineData("https://example.com/not-a-video")] // not a stream, not YouTube
    public void An_unset_or_unrecognised_url_classifies_as_null(string? url)
    {
        Assert.Null(HeroMedia.Classify(url));
    }
}
