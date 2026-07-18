using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public "Opening ceremony" page (/programme/opening —
// Figma 5867-22242). The first Programme-cluster page: an overview of the
// forum's activities (highlight cards) + the target participant segments
// (numbered list). Section headers come from resx (Opening.* keys); the list
// content lives here as Bilingual so one model renders in either language.
public partial class Opening
{
    // "Overview" (نظرة عامة) — eight forum-activity highlights, icon + label.
    public static readonly IReadOnlyList<Bilingual> Highlights =
    [
        new("ورش عمل عامة وخاصة", "Public & private workshops"),
        new("ثلاثة أيام معرض", "A three-day exhibition"),
        new("جلسات علمية وحوارية", "Scientific & panel sessions"),
        new("ثلاثة أيام ملتقى مع الافتتاح", "Three forum days incl. the opening"),
        new("لقاءات خاصة مع الجمعيات الدولية", "Private meetings with international associations"),
        new("فعاليات مصاحبة", "Side events"),
        new("لقاءات إعلامية", "Media briefings"),
        new("لقاءات ثنائية B2B", "B2B bilateral meetings"),
    ];

    // "Target participants" (الجهات المستهدفة للمشاركة) — the segments invited to
    // take part. (The Figma listed a 10th slot that duplicated #8; rendered the
    // nine distinct segments — see web/opening.md §7.)
    public static readonly IReadOnlyList<Bilingual> Participants =
    [
        new("الجهات الحكومية والهيئات الملكية في مناطق المملكة.",
            "Government bodies & royal commissions across the Kingdom's regions."),
        new("ملاك ومنظمو المعارض والمؤتمرات وفعاليات الأعمال محليًا ودوليًا.",
            "Owners & organisers of exhibitions, conferences & business events, locally & internationally."),
        new("ملاك قاعات المعارض والمؤتمرات والفنادق.",
            "Owners of exhibition & conference halls and hotels."),
        new("الجمعيات المهنية والعلمية والمنظمات المحلية والدولية.",
            "Professional & scientific associations & local & international organisations."),
        new("قطاع السياحة والضيافة والرياضة والثقافة.",
            "The tourism, hospitality, sport & culture sector."),
        new("الموردون والمصنعون (أنظمة AV، الهدايا، تصنيع أجنحة المعارض والديكور).",
            "Suppliers & manufacturers (AV systems, gifts, exhibition-stand fabrication & decor)."),
        new("مكاتب الاستشارات المهنية الخاصة بالمعارض والمؤتمرات.",
            "Professional consultancy offices for exhibitions & conferences."),
        new("مقدمو الخدمات (اللوجستية، التسويقية، الإعلامية، التقنية، إدارة الحشود).",
            "Service providers (logistics, marketing, media, technology & crowd management)."),
        new("الجهات وصناديق الاستثمار الخاصة بدعم وتطوير صناعة المعارض والمؤتمرات.",
            "Investment bodies & funds supporting & developing the exhibitions & conferences industry."),
    ];
}
