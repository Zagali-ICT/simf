using SIMF.Common;
using SIMF.Contracts;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Cms;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Media;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Sponsors;
using SIMF.Web.Endpoints;

namespace SIMF.Web.Tests;

/// <summary>Tests for the /content/site reshape (the landing's dynamic content
/// proxy) — covers the bilingual emission, the section-omitted-when-empty rule,
/// the sponsor+media-partner merge, and the all-or-nothing hero gate.</summary>
public sealed class SiteContentMapperTests
{
    // The shared key list, so adding a hero field can't silently desync the test.
    private static readonly string[] HeroKeys = LandingHeroContentKeys.All;

    [Fact]
    public void Empty_inputs_produce_an_empty_document()
    {
        var result = SiteContentEndpoints.Compose(null, null, null, null, null, null, null, null);

        Assert.Empty(result);
    }

    [Fact]
    public void Speakers_are_emitted_bilingually_arabic_as_base()
    {
        var speakers = new PublicSpeakers(new[]
        {
            new PublicSpeakerSummary(
                Guid.NewGuid(), "Jane Roe", "جين رو", "Admiral",
                null, null, "United States", "الولايات المتحدة", null, 0),
        });

        var result = SiteContentEndpoints.Compose(null, speakers, null, null, null, null, null, null);

        var first = Row(result, "speakers", 0);
        Assert.Equal("جين رو", first["name"]);
        Assert.Equal("Jane Roe", first["name_en"]);
        Assert.Equal("Admiral", first["role"]);
        Assert.Equal("الولايات المتحدة", first["org"]);
        Assert.Equal("United States", first["org_en"]);
    }

    // The asset is the only source of a speaker portrait. `photoRelativePath`
    // still rides the wire because the shipped app decodes the key, but nothing
    // can write it any more and the Website must not render it: a value arriving
    // in that field is stale data, not a fallback.
    [Fact]
    public void Speaker_photo_comes_from_the_asset_proxy_and_never_from_the_legacy_path()
    {
        var withAsset = Guid.NewGuid();
        var speakers = new PublicSpeakers(new[]
        {
            // Has an uploaded/linked asset → the same-origin asset proxy URL.
            new PublicSpeakerSummary(withAsset, "A", "أ", null, null, null, null, null,
                null, 0, HasPhotoAsset: true),
            // No asset, but a legacy portrait path still set → NO photo key.
            // This is the case the old fallback served and the one that changed.
            new PublicSpeakerSummary(Guid.NewGuid(), "B", "ب", null, null, null, null, null,
                "/legacy/b.jpg", 1, HasPhotoAsset: false),
            // Neither → no photo key (the card renders its silhouette).
            new PublicSpeakerSummary(Guid.NewGuid(), "C", "ج", null, null, null, null, null,
                null, 2, HasPhotoAsset: false),
        });

        var result = SiteContentEndpoints.Compose(null, speakers, null, null, null, null, null, null);

        Assert.Equal($"/content/assets/SpeakerPhoto/{withAsset}/image", Row(result, "speakers", 0)["photo"]);
        Assert.False(Row(result, "speakers", 1).ContainsKey("photo"));
        Assert.False(Row(result, "speakers", 2).ContainsKey("photo"));
    }

    [Fact]
    public void Partners_merge_sponsors_first_then_media_partners()
    {
        var sponsors = new PublicSponsors(new[]
        {
            new PublicSponsorTierGroup(1, "Platinum", new[]
            {
                new PublicSponsor(Guid.NewGuid(), "Acme", "أكمي", 1, "Platinum", null, null, 0),
            }),
        });
        var mediaPartners = new PublicMediaPartners(new[]
        {
            new PublicMediaPartnerItem(Guid.NewGuid(), "Globe News", "غلوب نيوز", null, null, 0),
        });

        var result = SiteContentEndpoints.Compose(null, null, sponsors, mediaPartners, null, null, null, null);

        var rows = Rows(result, "partners");
        Assert.Equal(2, rows.Count);

        var sponsor = Cast(rows[0]);
        Assert.Equal("أكمي", sponsor["name"]);
        Assert.Equal("Sponsor", sponsor["type_en"]);
        // D-348 — sponsors render a per-name SVG logo placeholder data-URI,
        // not an empty string.
        var sponsorLogo = Assert.IsType<string>(sponsor["logo"]);
        Assert.StartsWith("data:image/svg+xml,", sponsorLogo);
        Assert.Contains("Acme", sponsorLogo);

        var partner = Cast(rows[1]);
        Assert.Equal("غلوب نيوز", partner["name"]);
        Assert.Equal("Media Partner", partner["type_en"]);
    }

    [Fact]
    public void Archive_maps_year_title_and_counts()
    {
        var archive = new PublicArchive(new[]
        {
            new PublicArchiveEdition(
                Guid.NewGuid(), 2024, "SIMF 2024", "سيمف 2024",
                "A great edition", "نسخة رائعة", 1200, 30, 45, null),
        });

        var result = SiteContentEndpoints.Compose(null, null, null, null, null, archive, null, null);

        var row = Row(result, "archive", 0);
        Assert.Equal(2024, row["year"]);
        Assert.Equal("سيمف 2024", row["title"]);
        Assert.Equal("A great edition", row["desc_en"]);
        Assert.Contains("1200", (string)row["date_en"]!);
        Assert.Contains("45", (string)row["date_en"]!);
    }

    [Fact]
    public void Spirit_uses_the_image_proxy_and_skips_items_without_an_image()
    {
        var withImage = Guid.NewGuid();
        var media = new PublicMediaPage(new[]
        {
            new PublicMediaItem(withImage, default, null, null, null, null, "/media/x/image", null, null, 0),
            new PublicMediaItem(Guid.NewGuid(), default, null, null, null, null, null, null, "https://v", 0),
        }, Total: 2, Skip: 0, Top: 25);

        var result = SiteContentEndpoints.Compose(null, null, null, null, null, null, media, null);

        var rows = Rows(result, "spirit");
        Assert.Single(rows);
        Assert.Equal($"/content/media/{withImage}/image", Cast(rows[0])["img"]);
    }

    [Fact]
    public void Hero_is_emitted_only_when_every_key_resolved()
    {
        var full = HeroKeys.ToDictionary(
            k => k,
            k => new PublicContentBlock(k, k + "-en", k + "-ar", DateTime.UnixEpoch));

        var result = SiteContentEndpoints.Compose(
            null, null, null, null, null, null, null,
            new PublicContentBlockBatch(full));

        var hero = Assert.IsType<Dictionary<string, object?>>(result["hero"]);
        Assert.Equal("hero.titlestart-ar", hero["titleStart"]);
        Assert.Equal("hero.titlestart-en", hero["titleStart_en"]);
        Assert.Equal("hero.ctasecondary-ar", hero["ctaSecondary"]);
    }

    [Fact]
    public void Hero_is_omitted_when_any_key_is_missing()
    {
        var partial = HeroKeys.Take(6).ToDictionary(
            k => k,
            k => new PublicContentBlock(k, k + "-en", k + "-ar", DateTime.UnixEpoch));

        var result = SiteContentEndpoints.Compose(
            null, null, null, null, null, null, null,
            new PublicContentBlockBatch(partial));

        Assert.False(result.ContainsKey("hero"));
    }

    [Fact]
    public void Landing_sections_are_projected_into_nested_bilingual_nodes()
    {
        var keys = new[]
        {
            LandingSectionContentKeys.AboutHeading,
            LandingSectionContentKeys.StatsCount1,
            LandingSectionContentKeys.Goal1Title,
        };
        var blocks = keys.ToDictionary(
            k => k,
            k => new PublicContentBlock(k, k + "-en", k + "-ar", DateTime.UnixEpoch));

        var result = SiteContentEndpoints.Compose(
            null, null, null, null, null, null, null,
            new PublicContentBlockBatch(blocks));

        // about.h2 -> result["about"]["h2"] / ["h2_en"], Arabic as base.
        var about = Assert.IsType<Dictionary<string, object?>>(result["about"]);
        Assert.Equal("about.h2-ar", about["h2"]);
        Assert.Equal("about.h2-en", about["h2_en"]);

        var stats = Assert.IsType<Dictionary<string, object?>>(result["stats"]);
        Assert.Equal("stats.count1-ar", stats["count1"]);

        // Two levels deep: goals.item1.t -> result["goals"]["item1"]["t"].
        var goals = Assert.IsType<Dictionary<string, object?>>(result["goals"]);
        var item1 = Assert.IsType<Dictionary<string, object?>>(goals["item1"]);
        Assert.Equal("goals.item1.t-ar", item1["t"]);
        Assert.Equal("goals.item1.t-en", item1["t_en"]);
    }

    [Fact]
    public void Landing_sections_omit_sections_with_no_returned_keys()
    {
        // Only an About key present — stats / goals / pillars (and hero, which
        // needs every key) must stay absent so the page keeps its own markup.
        var blocks = new Dictionary<string, PublicContentBlock>
        {
            [LandingSectionContentKeys.AboutEyebrow] = new(
                LandingSectionContentKeys.AboutEyebrow, "About", "حول",
                DateTime.UnixEpoch),
        };

        var result = SiteContentEndpoints.Compose(
            null, null, null, null, null, null, null,
            new PublicContentBlockBatch(blocks));

        Assert.True(result.ContainsKey("about"));
        Assert.False(result.ContainsKey("stats"));
        Assert.False(result.ContainsKey("goals"));
        Assert.False(result.ContainsKey("pillars"));
        Assert.False(result.ContainsKey("hero"));
    }

    [Fact]
    public void Social_links_emit_only_the_configured_platforms()
    {
        // D-466 — only non-blank social URLs reach the feed; the footer renderer
        // hides any platform that is absent.
        var settings = new SiteSettingsResponse(
            "مرحبا", "Welcome",
            new SocialLinks(
                Facebook: null,
                X: "https://x.com/simf",
                Instagram: "   ", // blank → omitted
                LinkedIn: "https://linkedin.com/company/simf",
                YouTube: null,
                TikTok: null,
                Snapchat: null));

        var result = SiteContentEndpoints.Compose(
            null, null, null, null, null, null, null, null, settings);

        var social = Assert.IsType<Dictionary<string, object?>>(result["social"]);
        Assert.Equal("https://x.com/simf", social["x"]);
        Assert.Equal("https://linkedin.com/company/simf", social["linkedin"]);
        Assert.False(social.ContainsKey("facebook")); // null omitted
        Assert.False(social.ContainsKey("instagram")); // blank omitted
    }

    [Fact]
    public void Social_section_is_absent_when_no_links_are_configured()
    {
        var settings = new SiteSettingsResponse(
            "م", "W",
            new SocialLinks(null, null, null, null, null, null, null));

        var result = SiteContentEndpoints.Compose(
            null, null, null, null, null, null, null, null, settings);

        Assert.False(result.ContainsKey("social"));
    }

    private static List<object> Rows(Dictionary<string, object?> result, string key) =>
        Assert.IsType<List<object>>(result[key]);

    private static Dictionary<string, object?> Row(Dictionary<string, object?> result, string key, int index) =>
        Cast(Rows(result, key)[index]);

    private static Dictionary<string, object?> Cast(object row) =>
        Assert.IsType<Dictionary<string, object?>>(row);
}
