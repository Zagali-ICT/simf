using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public "About US" page (/about — Figma 5865-33963).
// Built on the shared LandingShell + LandingPageHero chrome. The page is
// marketing prose (no API): section headers come from resx, and the repeated
// card content lives here as Bilingual so one model renders in either language
// (the view resolves .For(rtl) for the active culture) — mirroring the landing.
//
// The About-intro block and the participation-stats band reuse the landing's
// existing content (Landing.About.* / Landing.Stats.* resx + Landing.Stats) so
// those event facts have a single source of truth rather than a second copy.
public partial class About
{
    // Forum values ("القيم") — a label-only card strip (icon + label).
    public static readonly IReadOnlyList<Bilingual> Values =
    [
        new("الابتكار", "Innovation"),
        new("التكامل والتواصل", "Integration & communication"),
        new("الاستدامة", "Sustainability"),
        new("المسؤولية", "Responsibility"),
    ];

    // Forum pillars ("ركائز الملتقى") — icon + title + description feature cards.
    public sealed record Pillar(string Icon, Bilingual Title, Bilingual Desc);

    public static readonly IReadOnlyList<Pillar> Pillars =
    [
        new("assets/figma/about/icon-globe.svg",
            new("حوار استراتيجي", "Strategic dialogue"),
            new("جلسات رفيعة المستوى تجمع الوزراء والقادة والخبراء.",
                "High-level sessions bringing together ministers, leaders and experts.")),
        new("assets/figma/about/icon-anchor.svg",
            new("شراكات عالمية", "Global partnerships"),
            new("بناء شراكات بين الجهات الحكومية والقطاع الصناعي.",
                "Building partnerships between government bodies and the industrial sector.")),
        new("assets/figma/about/icon-chip.svg",
            new("استشراف المستقبل", "Foreseeing the future"),
            new("استعراض أحدث التقنيات والحلول في القطاع البحري.",
                "Showcasing the latest technologies and solutions in the maritime sector.")),
    ];
}
