// D-756 — bUnit render proof that the landing hero background is config-driven
// from OrganizationProfile.BackgroundVideoUrl: a YouTube link renders a covering
// muted/loop/no-controls youtube-nocookie <iframe>; a direct MP4/HLS link renders
// the hero <video src>; an unset value keeps the bundled assets/hero-video.mp4.
// Renders the real Landing page against a stubbed public API (same envelope shape
// ForumDatesTests uses), so this exercises HeroMedia -> Landing -> DOM end-to-end.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts;
using SIMF.Contracts.Organization;
using SIMF.Web.Components.Pages;
using SIMF.Web.Content;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class LandingHeroTests : WebComponentTestBase
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public LandingHeroTests()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));
    }

    [Fact]
    public void A_youtube_background_video_renders_a_covering_iframe()
    {
        RegisterProfile(backgroundVideoUrl: "https://youtu.be/rmW5sJTp-Zo");

        var cut = RenderComponent<Landing>();

        var iframe = cut.Find("iframe.ln-hero__video--yt");
        var src = iframe.GetAttribute("src")!;
        Assert.Contains("youtube-nocookie.com/embed/rmW5sJTp-Zo", src);
        Assert.Contains("autoplay=1", src);
        Assert.Contains("mute=1", src);
        Assert.Contains("loop=1", src);
        Assert.Contains("controls=0", src);
        Assert.Empty(cut.FindAll("video.ln-hero__video"));
    }

    [Fact]
    public void A_direct_file_renders_the_hero_video_source()
    {
        RegisterProfile(backgroundVideoUrl: "https://cdn.example.com/hero.mp4");

        var cut = RenderComponent<Landing>();

        var video = cut.Find("video.ln-hero__video");
        Assert.Equal("https://cdn.example.com/hero.mp4", video.GetAttribute("src"));
        Assert.Empty(cut.FindAll("iframe.ln-hero__video--yt"));
    }

    [Fact]
    public void No_configured_video_keeps_the_bundled_asset()
    {
        RegisterProfile(backgroundVideoUrl: null);

        var cut = RenderComponent<Landing>();

        var video = cut.Find("video.ln-hero__video");
        Assert.Equal("assets/hero-video.mp4", video.GetAttribute("src"));
        Assert.Empty(cut.FindAll("iframe.ln-hero__video--yt"));
    }

    // The test above proves the MARKUP falls back to assets/hero-video.mp4, but a
    // green render says nothing about the file being there — delete the asset and
    // that test still passes while the public landing hero 404s. This asserts the
    // bundled fallback actually ships, so the pair covers both halves.
    [Fact]
    public void The_bundled_fallback_asset_exists()
    {
        var asset = Path.Combine(
            RepoRoot(), "src", "Website", "SIMF.Web", "wwwroot", "assets", "hero-video.mp4");

        Assert.True(
            File.Exists(asset),
            $"The bundled hero fallback is missing: {asset}. Landing.razor renders "
            + "`HeroVideo?.FileUrl ?? \"assets/hero-video.mp4\"`, so without this file "
            + "the public landing hero 404s whenever the CP has no video configured "
            + "or the organization profile is unavailable (D-756).");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
    }

    private void RegisterProfile(string? backgroundVideoUrl)
    {
        var client = new SimfPublicClient(new HttpClient(new StubHandler(backgroundVideoUrl))
        {
            BaseAddress = new Uri("https://api.test/"),
        });
        Services.AddSingleton(client);
        Services.AddScoped<HeroMedia>();
    }

    private sealed class StubHandler(string? backgroundVideoUrl) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = ApiResult<OrganizationProfileResponse>.Ok(new OrganizationProfileResponse(
                Name: "SIMF", NameArabic: "الملتقى", Title: "SIMF", TitleArabic: "الملتقى",
                Slogan: null, SloganArabic: null, Bio: null, BioArabic: null,
                Version: null, VersionDate: null, SysVersion: null, ReleaseDate: null,
                EventStartDate: null, EventEndDate: null, CurrentYear: 2026, Status: "Open",
                LocationText: null, LocationTextArabic: null, Latitude: null, Longitude: null,
                ContactPhone: null, ContactEmail: null, ContactWebsite: null, LiveStreamUrl: null,
                BackgroundVideoUrl: backgroundVideoUrl,
                Social: new SocialLinks(null, null, null, null, null, null, null),
                LogoUrl: null,
                AboutItems: Array.Empty<OrganizationAboutItemDto>(),
                Details: Array.Empty<OrganizationDetailDto>()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(result, Web), Web)),
            });
        }
    }
}
