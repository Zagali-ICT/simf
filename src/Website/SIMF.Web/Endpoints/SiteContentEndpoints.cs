// The public marketing landing (wwwroot/index.html) is a static client-rendered
// page with a designed-in remote-content hook (content.js -> loadSiteContentRemote).
// This endpoint is that hook's data source: a same-origin GET that server-side
// reads the API's anonymous public endpoints and reshapes them into the exact
// JSON shape the landing's renderers consume (the SITE_DEFAULTS shape). It is a
// same-origin proxy (the API has no CORS policy, so the browser cannot call it
// directly), mirroring the BFF proxy pattern in AccountEndpoints.
//
// Bilingual convention (matches content.js pickLang / getCmsValue): every text
// field is emitted twice — `field` carries the Arabic-preferred display value and
// `field_en` carries the English one. A section is only included when it has at
// least one row, so an unreachable API (or a genuinely empty section) leaves the
// landing on its built-in SITE_DEFAULTS rather than blanking.
using System.Globalization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Cms;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Media;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Sponsors;

namespace SIMF.Web.Endpoints;

internal static class SiteContentEndpoints
{
    // A neutral branded placeholder (navy card + gold "SIMF") for the two
    // sections whose renderers need a background image but whose API rows carry
    // none (sessions, news). Already URL-encoded so it drops straight into the
    // renderers' url('...') without further escaping.
    private const string PlaceholderImage =
        "data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20"
        + "viewBox='0%200%20800%20600'%3E%3Crect%20width='800'%20height='600'%20"
        + "fill='%23001640'/%3E%3Ctext%20x='400'%20y='330'%20fill='%23E8C060'%20"
        + "font-family='sans-serif'%20font-size='96'%20text-anchor='middle'%20"
        + "opacity='0.45'%3ESIMF%3C/text%3E%3C/svg%3E";

    // A per-partner branded logo placeholder (white card + the partner name in
    // navy) as a self-contained data-URI — so the partner strip always renders
    // a real, distinct logo image with no external dependency or stored asset
    // (the entity's LogoRelativePath is not publicly servable). textLength fits
    // any name to the card width. Uses the Latin name (renders cleanly in SVG).
    private static string PartnerLogoPlaceholder(string? name)
    {
        var text = Uri.EscapeDataString((name ?? string.Empty).Trim());
        return "data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20"
            + "viewBox='0%200%20260%20130'%3E"
            + "%3Crect%20width='260'%20height='130'%20rx='10'%20fill='%23ffffff'/%3E"
            + "%3Ctext%20x='130'%20y='72'%20fill='%23001640'%20"
            + "font-family='sans-serif'%20font-size='18'%20font-weight='700'%20"
            + "text-anchor='middle'%20textLength='220'%20"
            + "lengthAdjust='spacingAndGlyphs'%3E" + text + "%3C/text%3E%3C/svg%3E";
    }

    // Landing hero CMS fields: (backend content-block key — lowercased, as the
    // CMS service normalises keys) -> (website nested field under `hero`).
    private static readonly (string Key, string Field)[] HeroFields =
    [
        (LandingHeroContentKeys.TitleStart, "titleStart"),
        (LandingHeroContentKeys.TitleHighlight, "titleHighlight"),
        (LandingHeroContentKeys.TitleEnd, "titleEnd"),
        (LandingHeroContentKeys.Tagline, "tagline"),
        (LandingHeroContentKeys.MetaDate, "metaDate"),
        (LandingHeroContentKeys.MetaVenue, "metaVenue"),
        (LandingHeroContentKeys.CtaSecondary, "ctaSecondary"),
    ];

    // The hero keys requested from the CMS batch — derived once from HeroFields
    // (constant) rather than rebuilt on every request.
    private static readonly string[] HeroKeys = HeroFields.Select(f => f.Key).ToArray();

    // Every landing CMS key requested in one batch: the hero keys plus the
    // editorial sections below it (About, stats strip, Pillars header, Goals).
    private static readonly string[] LandingCmsKeys =
        [.. HeroKeys, .. LandingSectionContentKeys.All];

    // Arabic / English cultures cached once for date formatting (the framework
    // caches GetCultureInfo, but holding the references avoids the lookup).
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");

    public static void MapSiteContentEndpoints(this IEndpointRouteBuilder routes)
    {
        // The landing's content feed. Anonymous, same-origin, cacheable.
        routes.MapGet("/content/site",
            async (SimfPublicClient api, HttpContext http, CancellationToken ct) =>
        {
            var content = await BuildAsync(api, ct);
            http.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Json(content);
        });

        // Re-streams one gallery image same-origin so the landing's <img>/CSS
        // background loads it without reaching the API origin directly. Mirrors
        // the ID-image proxy in AccountEndpoints.
        routes.MapGet("/content/media/{id:guid}/image",
            async (Guid id, SimfPublicClient api, HttpContext http, CancellationToken ct) =>
        {
            var (status, contentType, bytes) = await api.FetchMediaImageAsync(id, ct);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.File(bytes, contentType);
        });

        // Re-streams one media-asset image same-origin (the unified Asset
        // pipeline, D-357) so a public page's <img>/CSS background loads it
        // without reaching the API origin directly. `category` is the
        // AssetCategory enum name; `ownerId` is the owning row's id. The API
        // serves the bytes, 302s to an external link, or 404s when none.
        routes.MapGet("/content/assets/{category}/{ownerId:guid}/image",
            async (string category, Guid ownerId, SimfPublicClient api, HttpContext http, CancellationToken ct) =>
        {
            var (status, contentType, bytes) = await api.FetchAssetImageAsync(category, ownerId, ct);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.File(bytes, contentType);
        });
    }

    private static async Task<Dictionary<string, object?>> BuildAsync(
        SimfPublicClient api, CancellationToken ct)
    {
        // Start every read together — they are independent anonymous GETs.
        var sessionsTask = api.GetProgrammeSessionsAsync(ct);
        var speakersTask = api.GetSpeakersAsync(ct);
        var newsTask = api.GetNewsAsync(1, 12, ct);
        var mediaPartnersTask = api.GetMediaPartnersAsync(ct);
        var sponsorsTask = api.GetSponsorsAsync(ct);
        var archiveTask = api.GetArchiveAsync(ct);
        var mediaTask = api.GetMediaAsync(null, 0, 24, ct);
        var cmsTask = api.GetContentBatchAsync(LandingCmsKeys, ct);
        var siteSettingsTask = api.GetSiteSettingsAsync(ct);

        await Task.WhenAll(
            sessionsTask, speakersTask, newsTask, mediaPartnersTask,
            sponsorsTask, archiveTask, mediaTask, cmsTask, siteSettingsTask);

        return Compose(
            sessionsTask.Result, speakersTask.Result, sponsorsTask.Result,
            mediaPartnersTask.Result, newsTask.Result, archiveTask.Result,
            mediaTask.Result, cmsTask.Result, siteSettingsTask.Result);
    }

    // The pure reshape from the API contracts to the landing's content model —
    // kept IO-free (no HttpClient) so it is unit-testable on its own.
    internal static Dictionary<string, object?> Compose(
        PublicSessions? sessions, PublicSpeakers? speakers, PublicSponsors? sponsors,
        PublicMediaPartners? mediaPartners, PublicNewsPage? news, PublicArchive? archive,
        PublicMediaPage? media, PublicContentBlockBatch? cms,
        SiteSettingsResponse? siteSettings = null)
    {
        var result = new Dictionary<string, object?>();

        AddIfAny(result, "sessions", MapSessions(sessions));
        AddIfAny(result, "speakers", MapSpeakers(speakers));
        AddIfAny(result, "partners", MapPartners(sponsors, mediaPartners));
        AddIfAny(result, "news", MapNews(news));
        AddIfAny(result, "archive", MapArchive(archive));
        AddIfAny(result, "spirit", MapSpirit(media));

        // D-466 — CP-editable footer social links (only configured URLs).
        var social = MapSocial(siteSettings);
        if (social.Count > 0)
        {
            result["social"] = social;
        }

        var hero = MapHero(cms);
        if (hero is not null)
        {
            result["hero"] = hero;
        }

        // The editorial sections below the hero (About, stats strip, Pillars
        // header, Goals). Each top-level section is emitted only when the CMS
        // returned at least one of its keys, so an unseeded section leaves the
        // landing on its built-in markup.
        foreach (var section in MapLandingSections(cms))
        {
            result[section.Key] = section.Value;
        }

        return result;
    }

    private static List<object> MapSessions(PublicSessions? sessions)
    {
        var rows = new List<object>();
        if (sessions is null)
        {
            return rows;
        }
        var index = 1;
        foreach (var s in sessions.Items)
        {
            var item = new Dictionary<string, object?> { ["n"] = index.ToString("D2"), ["img"] = PlaceholderImage };
            PutBilingual(item, "label", s.HallNameArabic, s.HallName);
            PutBilingual(item, "tag", s.PrimaryThemeNameArabic ?? s.CategoryNameArabic,
                                      s.PrimaryThemeName ?? s.CategoryName);
            PutBilingual(item, "title", s.TitleArabic, s.Title);
            PutBilingual(item, "desc", s.DescriptionArabic, s.Description);
            rows.Add(item);
            index++;
        }
        return rows;
    }

    private static List<object> MapSpeakers(PublicSpeakers? speakers)
    {
        var rows = new List<object>();
        if (speakers is null)
        {
            return rows;
        }
        foreach (var sp in speakers.Items)
        {
            var item = new Dictionary<string, object?>();
            PutBilingual(item, "name", sp.NameArabic, sp.Name);
            // Rank is a single (non-bilingual) line; emit it for both locales.
            PutBilingual(item, "role", sp.Rank, sp.Rank);
            PutBilingual(item, "org", sp.CountryNameAr, sp.CountryNameEn);
            // Portrait URL — prefer an uploaded/linked SpeakerPhoto asset from the
            // unified media-asset pipeline (D-357), served same-origin through the
            // /content/assets proxy; otherwise fall back to the legacy portrait path
            // (D-346). The card renders whichever is present, else its SVG silhouette.
            if (sp.HasPhotoAsset)
            {
                item["photo"] = $"/content/assets/SpeakerPhoto/{sp.Id}/image";
            }
            else if (!string.IsNullOrWhiteSpace(sp.PhotoRelativePath))
            {
                item["photo"] = sp.PhotoRelativePath;
            }
            rows.Add(item);
        }
        return rows;
    }

    // Sponsors first (already tier-ordered, highest tier first), then media
    // partners. `logo` carries the entity's LogoRelativePath when present so the
    // landing's partner card renders the logo image (else it falls back to the
    // partner name text). Currently a test placeholder seeded on the rows.
    private static List<object> MapPartners(PublicSponsors? sponsors, PublicMediaPartners? mediaPartners)
    {
        var rows = new List<object>();
        if (sponsors is not null)
        {
            foreach (var group in sponsors.Groups)
            {
                foreach (var sp in group.Sponsors)
                {
                    var item = new Dictionary<string, object?> { ["logo"] = PartnerLogoPlaceholder(sp.NameEn) };
                    PutBilingual(item, "name", sp.NameAr, sp.NameEn);
                    item["type"] = "راعٍ";
                    item["type_en"] = "Sponsor";
                    rows.Add(item);
                }
            }
        }
        if (mediaPartners is not null)
        {
            foreach (var mp in mediaPartners.Items)
            {
                var item = new Dictionary<string, object?> { ["logo"] = PartnerLogoPlaceholder(mp.Name) };
                PutBilingual(item, "name", mp.NameArabic, mp.Name);
                item["type"] = "شريك إعلامي";
                item["type_en"] = "Media Partner";
                rows.Add(item);
            }
        }
        return rows;
    }

    private static List<object> MapNews(PublicNewsPage? news)
    {
        var rows = new List<object>();
        if (news is null)
        {
            return rows;
        }
        foreach (var n in news.Items)
        {
            var item = new Dictionary<string, object?> { ["img"] = PlaceholderImage };
            item["date"] = FormatDate(n.PublishedAt, ArabicCulture);
            item["date_en"] = FormatDate(n.PublishedAt, EnglishCulture);
            PutBilingual(item, "title", n.TitleArabic, n.Title);
            PutBilingual(item, "excerpt", n.ExcerptArabic, n.Excerpt);
            rows.Add(item);
        }
        return rows;
    }

    private static List<object> MapArchive(PublicArchive? archive)
    {
        var rows = new List<object>();
        if (archive is null)
        {
            return rows;
        }
        foreach (var a in archive.Items)
        {
            var item = new Dictionary<string, object?> { ["year"] = a.Year };
            // D-347 — id + full detail so the per-year archive page renders the
            // mockup (screen 24-01) layout from the data already in /content/site.
            item["id"] = a.Id;
            item["date"] = $"{a.Attendees} حضور · {a.Speakers} متحدث";
            item["date_en"] = $"{a.Attendees} attendees · {a.Speakers} speakers";
            PutBilingual(item, "title", a.TitleAr, a.TitleEn);
            PutBilingual(item, "desc", a.SummaryAr, a.SummaryEn);
            PutBilingual(item, "location", a.LocationAr, a.LocationEn);
            PutBilingual(item, "dateLabel", a.DateLabelAr, a.DateLabelEn);
            item["attendees"] = a.Attendees;
            item["sessions"] = a.Sessions;
            item["speakers"] = a.Speakers;
            rows.Add(item);
        }
        return rows;
    }

    // D-466 — the CP-editable footer social links. Only non-empty URLs are
    // emitted; the landing's footer renderer hides any platform that is absent.
    private static Dictionary<string, object?> MapSocial(SiteSettingsResponse? settings)
    {
        var social = new Dictionary<string, object?>();
        if (settings is null)
        {
            return social;
        }
        void Put(string key, string? url)
        {
            if (!string.IsNullOrWhiteSpace(url)) { social[key] = url.Trim(); }
        }
        Put("facebook", settings.Social.Facebook);
        Put("x", settings.Social.X);
        Put("instagram", settings.Social.Instagram);
        Put("linkedin", settings.Social.LinkedIn);
        Put("youtube", settings.Social.YouTube);
        Put("tiktok", settings.Social.TikTok);
        Put("snapchat", settings.Social.Snapchat);
        return social;
    }

    private static List<object> MapSpirit(PublicMediaPage? media)
    {
        var rows = new List<object>();
        if (media is null)
        {
            return rows;
        }
        foreach (var m in media.Items)
        {
            // Only items that actually carry an image; routed through the
            // same-origin image proxy below.
            if (m.ImageUrl is null)
            {
                continue;
            }
            rows.Add(new Dictionary<string, object?> { ["img"] = $"/content/media/{m.Id}/image" });
        }
        return rows;
    }

    // Returns the hero object only when EVERY landing hero key resolved, so the
    // landing never ends up with a half-populated hero (its blunt remote merge
    // replaces the whole `hero` object). A missing batch or any missing key
    // leaves the hero on its built-in defaults.
    private static Dictionary<string, object?>? MapHero(PublicContentBlockBatch? batch)
    {
        if (batch is null)
        {
            return null;
        }
        var hero = new Dictionary<string, object?>();
        foreach (var (key, field) in HeroFields)
        {
            if (!batch.Blocks.TryGetValue(key, out var block))
            {
                return null;
            }
            hero[field] = string.IsNullOrEmpty(block.ContentArabic) ? block.Content : block.ContentArabic;
            hero[field + "_en"] = string.IsNullOrEmpty(block.Content) ? block.ContentArabic : block.Content;
        }
        return hero;
    }

    // Builds the editorial-section objects (about/stats/pillars/goals) from the
    // CMS batch by projecting each dotted key into a nested bilingual node —
    // e.g. "goals.item1.t" -> result["goals"]["item1"]["t"]/["t_en"], which is
    // exactly the path the landing's getCmsValue() reads. Only keys the CMS
    // actually returned are emitted, so an unseeded key leaves that field on the
    // page's built-in markup, and a section with no returned keys is absent
    // entirely. A null batch yields an empty dictionary.
    private static Dictionary<string, object?> MapLandingSections(PublicContentBlockBatch? batch)
    {
        var sections = new Dictionary<string, object?>();
        if (batch is null)
        {
            return sections;
        }
        foreach (var key in LandingSectionContentKeys.All)
        {
            if (!batch.Blocks.TryGetValue(key, out var block))
            {
                continue;
            }
            PutNestedBilingual(sections, key, block.ContentArabic, block.Content);
        }
        return sections;
    }

    // Sets a dotted key into nested dictionaries, writing the leaf twice — the
    // Arabic-preferred display value under the bare field and the English value
    // under `<field>_en` — matching PutBilingual's convention.
    private static void PutNestedBilingual(
        Dictionary<string, object?> root, string dottedKey, string? arabic, string? english)
    {
        var parts = dottedKey.Split('.');
        var node = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (node.TryGetValue(parts[i], out var existing)
                && existing is Dictionary<string, object?> childDict)
            {
                node = childDict;
                continue;
            }
            var created = new Dictionary<string, object?>();
            node[parts[i]] = created;
            node = created;
        }
        var leaf = parts[^1];
        var ar = arabic ?? string.Empty;
        var en = english ?? string.Empty;
        node[leaf] = ar.Length > 0 ? ar : en;
        node[leaf + "_en"] = en.Length > 0 ? en : ar;
    }

    // base (Arabic display) prefers the Arabic value, falling back to English so
    // it is never empty; `_en` mirrors that the other way.
    private static void PutBilingual(
        Dictionary<string, object?> item, string field, string? arabic, string? english)
    {
        var ar = arabic ?? string.Empty;
        var en = english ?? string.Empty;
        item[field] = ar.Length > 0 ? ar : en;
        item[field + "_en"] = en.Length > 0 ? en : ar;
    }

    private static string FormatDate(DateTimeOffset value, CultureInfo culture) =>
        value.ToString("d MMMM yyyy", culture);

    private static void AddIfAny(Dictionary<string, object?> result, string key, List<object> rows)
    {
        if (rows.Count > 0)
        {
            result[key] = rows;
        }
    }
}
