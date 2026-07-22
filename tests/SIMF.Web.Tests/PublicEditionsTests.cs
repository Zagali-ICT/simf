// Unit tests for PublicEditions.Build / .Label (SIMF.Web) — the single source shared
// by the /archive page cards and the top-nav Archive dropdown. Build orders live
// editions newest-first, assigns the index-based anchor id the card renders and the
// dropdown links to (/archive#ed-N), labels them "title year", carries the latest
// edition's headline stats, and falls back to the landing's static Milestones when
// the archive is empty. Pure + internal, so no HTTP stub is needed.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Archive;
using SIMF.Web.Content;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class PublicEditionsTests
{
    [Fact]
    public async Task GetAsync_does_not_cache_a_transient_failure_but_caches_a_real_answer()
    {
        var handler = new SwitchableHandler();
        var api = new SimfPublicClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new PublicEditions(api, cache);

        // 1. A transient failure (503 -> the client returns null) serves the static
        //    fallback and must NOT be cached.
        handler.Mode = SwitchableHandler.Fail;
        var first = await sut.GetAsync();
        Assert.Contains("2024", first.Editions[0].NavLabel.En); // Milestones fallback

        // 2. The API recovers -> the next call returns the LIVE edition, proving the
        //    failure was not cached over the live source.
        handler.Mode = SwitchableHandler.Live;
        var second = await sut.GetAsync();
        Assert.Equal("SIMF 2030", second.Editions[0].NavLabel.En);

        // 3. A real answer IS cached -> a later failure still returns it (not the fallback).
        handler.Mode = SwitchableHandler.Fail;
        var third = await sut.GetAsync();
        Assert.Equal("SIMF 2030", third.Editions[0].NavLabel.En);
    }

    [Fact]
    public void Build_orders_newest_first_with_aligned_anchor_href_label_and_latest_stats()
    {
        var view = PublicEditions.Build(new[]
        {
            Edition(2022, "SIMF", attendees: 1000, sessions: 30, speakers: 200),
            Edition(2025, "SIMF", attendees: 1500, sessions: 50, speakers: 300),
            Edition(2024, "SIMF", attendees: 1200, sessions: 40, speakers: 250),
        });

        Assert.Equal(3, view.Editions.Count);
        // newest-first, labelled "title year"
        Assert.Equal("SIMF 2025", view.Editions[0].NavLabel.En);
        Assert.Equal("SIMF 2024", view.Editions[1].NavLabel.En);
        Assert.Equal("SIMF 2022", view.Editions[2].NavLabel.En);
        // index-based anchor + href aligned with the /archive cards
        Assert.Equal("ed-0", view.Editions[0].AnchorId);
        Assert.Equal("/archive#ed-0", view.Editions[0].Href);
        Assert.Equal("/archive#ed-2", view.Editions[2].Href);
        // headline stats come from the latest edition (2025)
        Assert.Equal("+300", view.Speakers);
        Assert.Equal("+1500", view.Attendees);
        Assert.Equal("+50", view.Sessions);
    }

    [Fact]
    public void Build_falls_back_to_the_static_past_editions_when_empty()
    {
        var view = PublicEditions.Build(Array.Empty<PublicArchiveEdition>());

        // the landing's three past (non-future) editions, newest-first, each anchored
        Assert.Equal(3, view.Editions.Count);
        Assert.Contains("2024", view.Editions[0].NavLabel.En);
        Assert.All(view.Editions, e => Assert.StartsWith("/archive#ed-", e.Href));
        // the default headline numbers keep the stats band populated
        Assert.Equal("+250", view.Speakers);
    }

    [Theory]
    [InlineData("SIMF", 2025, "SIMF 2025")]      // year appended
    [InlineData("SIMF 2025", 2025, "SIMF 2025")] // already carries the year → unchanged
    [InlineData("Edition", 0, "Edition")]        // no year → title as-is
    public void Label_appends_the_year_unless_already_present(string title, int year, string expected)
    {
        var label = PublicEditions.Label(new Bilingual(title, title), year);

        Assert.Equal(expected, label.En);
    }

    private static PublicArchiveEdition Edition(
        int year, string title, int attendees, int sessions, int speakers) =>
        new(Guid.NewGuid(), year, title, title, SummaryEn: null, SummaryAr: null,
            attendees, sessions, speakers, CoverImageRelativePath: null);

    // Flips between a transient failure (503 -> SimfPublicClient returns null) and a
    // successful one-edition archive, so the caching contract can be exercised.
    private sealed class SwitchableHandler : HttpMessageHandler
    {
        public const int Fail = 0;
        public const int Live = 1;
        public int Mode { get; set; } = Fail;

        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Mode == Fail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            var archive = ApiResult<PublicArchive>.Ok(new PublicArchive(new[]
            {
                Edition(2030, "SIMF", attendees: 100, sessions: 10, speakers: 20),
            }));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(archive, Web), Web)),
            });
        }
    }
}
