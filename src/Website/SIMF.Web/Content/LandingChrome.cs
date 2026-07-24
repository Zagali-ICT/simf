namespace SIMF.Web.Content;

// Shared content model + chrome data for the public "ln-" marketing pages
// (landing, speakers, about, programme, …). Extracted from Landing so every
// page renders one copy of the nav + footer via the shared LandingHeader /
// LandingFooter components instead of duplicating the markup.
//
// Bilingual carries AR/EN so a single model renders in either language; chrome
// labels are stored as resx KEYS (not literal text) and resolved through
// IStringLocalizer for the active culture — mirroring the backend
// field/field_en convention in SiteContentEndpoints.
public sealed record Bilingual(string Ar, string En)
{
    public string For(bool rtl) => rtl ? Ar : En;
}

// ---- Navigation ---------------------------------------------------------
// LabelKey is a resx key resolved through IStringLocalizer. Label carries literal
// AR/EN text instead (for data-driven items such as the Archive edition list,
// whose labels come from the API, not resx); when set it wins over LabelKey.
public sealed record NavLink(string LabelKey, string Href, Bilingual? Label = null);

// A menu with Links renders as a hover/focus dropdown; a menu with only a
// Href (Links empty) renders as a plain top-level link.
public sealed record NavMenu(
    string LabelKey, string TitleKey, IReadOnlyList<NavLink> Links, string? Href = null);

// ---- Footer -------------------------------------------------------------
public sealed record FooterLink(Bilingual Label, string Href);

// The public-site chrome data: top-nav mega-menu, search-panel chips and the
// footer's external-links group. Shared by LandingHeader / LandingFooter.
public static class LandingChrome
{
    public static readonly IReadOnlyList<NavMenu> NavMenus =
    [
        new("Landing.Nav.About", "Landing.Nav.About.Title",
        [
            new("Landing.Nav.About.Overview", "/about"),
            new("Landing.Nav.About.Goals", "/about/objectives"),
            new("Landing.Nav.About.Themes", "/about/themes"),
            new("Landing.Nav.About.Organizer", "/about/organizer"),
            new("Landing.Nav.About.Partnerships", "/partners"),
            new("Landing.Nav.About.Venue", "/about/venue"),
        ]),
        new("Landing.Nav.Programs", "Landing.Nav.Programs.Title",
        [
            new("Landing.Nav.Programs.Agenda", "/programme"),
            new("Landing.Nav.Programs.Opening", "/programme/opening"),
            new("Landing.Nav.Programs.Sessions", "/programme/sessions"),
            new("Landing.Nav.Programs.Exhibition", "/programme/exhibition"),
            new("Landing.Nav.Programs.GovMeetings", "/programme/gov-meetings"),
            new("Landing.Nav.Programs.Visit", "/visit"),
        ]),
        new("Landing.Nav.Speakers", "Landing.Nav.Speakers", [], Href: "/speakers"),
        // "Discover Saudi Arabia" → the official Visit Saudi site, matching the
        // Flutter app's single روح السعودية link (BuildConfig.visitSaudiUrl,
        // default https://www.visitsaudi.com). A single external top-level link
        // (opens in a new tab) — not a dropdown of dead in-page anchors.
        new("Landing.Nav.Discover", "Landing.Nav.Discover", [], Href: "https://www.visitsaudi.com"),
        // "Archive" → the real /archive page, which lists every past edition.
        // A single top-level link (was a dropdown of dead "#" edition anchors).
        new("Landing.Nav.Archive", "Landing.Nav.Archive", [], Href: "/archive"),
    ];

    // Search-panel suggestion chips.
    public static readonly IReadOnlyList<string> SearchChips =
    [
        "Landing.Search.Chip.Articles",
        "Landing.Search.Chip.Services",
        "Landing.Search.Chip.Training",
    ];

    // Footer "important links" (external government sites).
    public static readonly IReadOnlyList<FooterLink> FooterImportantLinks =
    [
        new(new("وزارة الدفاع", "Ministry of Defense"), "https://mod.gov.sa/ar/Pages/default.aspx"),
        new(new("الهيئة العامة للصناعات العسكرية", "General Authority for Military Industries"), "https://www.gami.gov.sa/ar"),
        new(new("الهيئة العامة للتطوير الدفاعي", "General Authority for Defense Development"), "https://www.gadd.gov.sa/"),
        new(new("المحتوى المحلي والمشتريات الحكومية", "Local Content & Government Procurement"), "https://lcgpa.gov.sa"),
        new(new("الشركة السعودية للصناعات العسكرية", "Saudi Arabian Military Industries"), "https://www.sami.com.sa/ar"),
    ];
}
