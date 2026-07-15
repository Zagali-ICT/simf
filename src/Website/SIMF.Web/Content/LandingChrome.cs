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
public sealed record NavLink(string LabelKey, string Href);

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
            new("Landing.Nav.About.Partnerships", "#partners"),
            new("Landing.Nav.About.Venue", "#"),
        ]),
        new("Landing.Nav.Programs", "Landing.Nav.Programs.Title",
        [
            new("Landing.Nav.Programs.Opening", "#"),
            new("Landing.Nav.Programs.Sessions", "#sessions"),
            new("Landing.Nav.Programs.Exhibition", "#"),
            new("Landing.Nav.Programs.GovMeetings", "#"),
            new("Landing.Nav.Programs.Visit", "#"),
        ]),
        new("Landing.Nav.Speakers", "Landing.Nav.Speakers", [], Href: "/speakers"),
        new("Landing.Nav.Discover", "Landing.Nav.Discover",
        [
            new("Landing.Nav.Discover.About", "#"),
            new("Landing.Nav.Discover.Invest", "#"),
            new("Landing.Nav.Discover.Spirit", "#"),
            new("Landing.Nav.Discover.Made", "#discover"),
        ]),
        new("Landing.Nav.Archive", "Landing.Nav.Archive",
        [
            new("Landing.Nav.Archive.E1", "#"),
            new("Landing.Nav.Archive.E2", "#"),
            new("Landing.Nav.Archive.E3", "#"),
        ]),
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
