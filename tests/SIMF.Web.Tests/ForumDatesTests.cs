// D-755 — unit tests for ForumDates (SIMF.Web): it resolves the CP-editable forum
// event dates from the anonymous public OrganizationProfile and formats the shared
// bilingual range, caching the read so the marketing pages don't re-hit the API on
// every render. SimfPublicClient is sealed over HttpClient, so a stub handler
// returns a canned ApiResult envelope serialised with the web defaults it reads.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts;
using SIMF.Contracts.Organization;
using SIMF.Web.Content;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class ForumDatesTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Formats_the_configured_dates_in_English_and_Arabic()
    {
        var handler = new StubHandler(Envelope(
            new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero)));
        var dates = NewForumDates(handler);

        Assert.Equal("23-25 November 2026", await dates.GetRangeDisplayAsync(arabic: false));
        Assert.Equal("23-25 نوفمبر 2026", await dates.GetRangeDisplayAsync(arabic: true));
    }

    [Fact]
    public async Task Caches_the_profile_read_across_calls()
    {
        var handler = new StubHandler(Envelope(
            new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero)));
        var dates = NewForumDates(handler);

        await dates.GetRangeDisplayAsync(arabic: false);
        await dates.GetRangeDisplayAsync(arabic: true);
        await dates.GetRangeDisplayAsync(arabic: false);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Returns_null_when_the_profile_is_unreachable()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var dates = NewForumDates(handler);

        Assert.Null(await dates.GetRangeDisplayAsync(arabic: false));
    }

    [Fact]
    public async Task Returns_null_when_the_profile_carries_no_dates()
    {
        var handler = new StubHandler(Envelope(start: null, end: null));
        var dates = NewForumDates(handler);

        Assert.Null(await dates.GetRangeDisplayAsync(arabic: false));
    }

    private static ForumDates NewForumDates(HttpMessageHandler handler) =>
        new(
            new SimfPublicClient(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.test/"),
            }),
            new MemoryCache(new MemoryCacheOptions()));

    // A canned OrganizationProfile envelope carrying just the two dates the
    // formatter reads (the rest of the profile is irrelevant here).
    private static Func<HttpRequestMessage, HttpResponseMessage> Envelope(
        DateTimeOffset? start, DateTimeOffset? end)
    {
        var result = ApiResult<OrganizationProfileResponse>.Ok(new OrganizationProfileResponse(
            Name: "SIMF", NameArabic: "الملتقى", Title: "SIMF", TitleArabic: "الملتقى",
            Slogan: null, SloganArabic: null, Bio: null, BioArabic: null,
            Version: null, VersionDate: null, SysVersion: null, ReleaseDate: null,
            EventStartDate: start, EventEndDate: end, CurrentYear: 2026, Status: "Open",
            LocationText: null, LocationTextArabic: null, Latitude: null, Longitude: null,
            ContactPhone: null, ContactEmail: null, ContactWebsite: null, LiveStreamUrl: null,
            Social: new SocialLinks(null, null, null, null, null, null, null),
            LogoUrl: null,
            AboutItems: Array.Empty<OrganizationAboutItemDto>(),
            Details: Array.Empty<OrganizationDetailDto>()));

        return _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(result, Web), Web)),
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}
