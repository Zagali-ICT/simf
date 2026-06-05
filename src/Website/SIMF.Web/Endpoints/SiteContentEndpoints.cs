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
        var cmsTask = api.GetContentBatchAsync(HeroKeys, ct);

        await Task.WhenAll(
            sessionsTask, speakersTask, newsTask, mediaPartnersTask,
            sponsorsTask, archiveTask, mediaTask, cmsTask);

        return Compose(
            sessionsTask.Result, speakersTask.Result, sponsorsTask.Result,
            mediaPartnersTask.Result, newsTask.Result, archiveTask.Result,
            mediaTask.Result, cmsTask.Result);
    }

    // The pure reshape from the API contracts to the landing's content model —
    // kept IO-free (no HttpClient) so it is unit-testable on its own.
    internal static Dictionary<string, object?> Compose(
        PublicSessions? sessions, PublicSpeakers? speakers, PublicSponsors? sponsors,
        PublicMediaPartners? mediaPartners, PublicNewsPage? news, PublicArchive? archive,
        PublicMediaPage? media, PublicContentBlockBatch? cms)
    {
        var result = new Dictionary<string, object?>();

        AddIfAny(result, "sessions", MapSessions(sessions));
        AddIfAny(result, "speakers", MapSpeakers(speakers));
        AddIfAny(result, "partners", MapPartners(sponsors, mediaPartners));
        AddIfAny(result, "news", MapNews(news));
        AddIfAny(result, "archive", MapArchive(archive));
        AddIfAny(result, "spirit", MapSpirit(media));

        var hero = MapHero(cms);
        if (hero is not null)
        {
            result["hero"] = hero;
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
            rows.Add(item);
        }
        return rows;
    }

    // Sponsors first (already tier-ordered, highest tier first), then media
    // partners. Logos are not publicly servable, so `logo` stays empty and the
    // landing's partner card falls back to the partner name text.
    private static List<object> MapPartners(PublicSponsors? sponsors, PublicMediaPartners? mediaPartners)
    {
        var rows = new List<object>();
        if (sponsors is not null)
        {
            foreach (var group in sponsors.Groups)
            {
                foreach (var sp in group.Sponsors)
                {
                    var item = new Dictionary<string, object?> { ["logo"] = string.Empty };
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
                var item = new Dictionary<string, object?> { ["logo"] = string.Empty };
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
            item["date"] = $"{a.Attendees} حضور · {a.Speakers} متحدث";
            item["date_en"] = $"{a.Attendees} attendees · {a.Speakers} speakers";
            PutBilingual(item, "title", a.TitleAr, a.TitleEn);
            PutBilingual(item, "desc", a.SummaryAr, a.SummaryEn);
            rows.Add(item);
        }
        return rows;
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
