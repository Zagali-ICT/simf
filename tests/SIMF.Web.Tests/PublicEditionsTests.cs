// Unit tests for PublicEditions.Build / .Label (SIMF.Web) — the single source shared
// by the /archive page cards and the top-nav Archive dropdown. Build orders live
// editions newest-first, assigns the index-based anchor id the card renders and the
// dropdown links to (/archive#ed-N), labels them "title year", carries the latest
// edition's headline stats, and falls back to the landing's static Milestones when
// the archive is empty. Pure + internal, so no HTTP stub is needed.
using SIMF.Contracts.Archive;
using SIMF.Web.Content;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class PublicEditionsTests
{
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
}
