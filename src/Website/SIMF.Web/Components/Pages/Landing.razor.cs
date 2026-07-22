using System.Globalization;
using Microsoft.AspNetCore.Components;
using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public landing. Holds the page's static content model —
// the lists the Razor view walks with @foreach. Labels are stored as resx KEYS
// (not literal text) so the one model renders in either language: the view
// resolves each key through IStringLocalizer for the active culture.
//
// The shared chrome data (nav mega-menu, search chips, footer links) and the
// Bilingual record now live in SIMF.Web.Content.LandingChrome — reused by every
// public "ln-" page (LandingHeader / LandingFooter / LandingShell).
public partial class Landing
{
    // D-755 — the CP-editable forum date range (from OrganizationProfile), resolved
    // server-side during static SSR. Null when the profile is unavailable; the view
    // then falls back to the Landing.Subnav.Date resx label.
    [Inject] private ForumDates Dates { get; set; } = default!;

    // D-756 — the CP-editable hero background video (from OrganizationProfile),
    // resolved server-side during static SSR. Null when none is configured / the
    // profile is unavailable; the view then falls back to the bundled hero-video.mp4.
    [Inject] private HeroMedia Hero { get; set; } = default!;

    private string? ForumDate { get; set; }
    private HeroVideoSource? HeroVideo { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ForumDate = await Dates.GetRangeDisplayAsync(
            CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft);
        HeroVideo = await Hero.GetAsync();
    }

    public sealed record ThreatStat(Bilingual Value, Bilingual Caption);

    // Rotating "threat landscape" marquee under the intro lead.
    public static readonly IReadOnlyList<ThreatStat> ThreatStats =
    [
        new(new("+300%", "+300%"), new("معدل ارتفاع الهجمات على البنية التحتية البحرية", "Rise in attacks on maritime infrastructure")),
        new(new("+200%", "+200%"), new("معدل التهديدات السيبرانية البحرية (خلال 5 سنوات)", "Rise in maritime cyber threats (past 5 years)")),
        new(new("+400", "+400"), new("كابل نشط يربط قارات العالم", "Active cables linking the world's continents")),
        new(new("+900 ألف كم", "900k+ km"), new("خطوط الأنابيب البحرية حول العالم", "Subsea pipelines worldwide")),
        new(new("+30%", "+30%"), new("من الغاز الطبيعي المسال يُنقل بحراً", "Of LNG is shipped by sea")),
        new(new("+50%", "+50%"), new("من تجارة البترول العالمية", "Of global oil trade")),
        new(new("30%–50%", "30%–50%"), new("معدل تأخر الرحلات بسبب الظروف البحرية", "Voyages delayed by sea conditions")),
        new(new("+350%", "+350%"), new("معدل ارتفاع تكاليف الشحن", "Rise in shipping costs")),
        new(new("100 مليون", "100M"), new("حاوية تجارية تتحرك عبر الموانئ أسبوعياً", "Containers move through ports weekly")),
        new(new("80%", "80%"), new("من السلع العالمية تُنقل بحراً", "Of global goods are carried by sea")),
        new(new("19 تريليون $ سنوياً", "$19T / year"), new("قيمة ما تنقله الكابلات البحرية", "Value carried by subsea cables")),
        new(new("1.4 مليون كم", "1.4M km"), new("حركة البيانات المالية عبر البحار", "Financial data traffic across the seas")),
    ];

    public sealed record StatCounter(string Num, Bilingual Label);

    // Participation counters in the navy band (Figma "مؤشرات المشاركة الدولية").
    public static readonly IReadOnlyList<StatCounter> Stats =
    [
        new("+500", new("فاعل ومسؤول", "Officials & delegates")),
        new("+40", new("دولة مشاركة", "Participating countries")),
        new("+100", new("متحدث دولي", "International speakers")),
        new("+220", new("جهة مشاركة", "Participating entities")),
    ];

    // ---- Milestones (past + future editions) -----------------------------
    public sealed record Milestone(string Image, Bilingual Date, Bilingual Name, Bilingual Text, bool IsFuture = false);

    public static readonly IReadOnlyList<Milestone> Milestones =
    [
        new("assets/figma/milestones/card4-secure-lanes.jpg",
            new("24–26 نوفمبر 2019", "24–26 November 2019"),
            new("تأمين الممرات البحرية", "Securing maritime lanes"),
            new("الرياض، المملكة العربية السعودية — انطلاق النسخة التأسيسية بمشاركة 35 دولة.",
                "Riyadh, Saudi Arabia — the founding edition launched with 35 countries.")),
        new("assets/figma/milestones/card2-digital.jpg",
            new("15–17 نوفمبر 2022", "15–17 November 2022"),
            new("التحول الرقمي", "Digital transformation"),
            new("جدة، المملكة العربية السعودية — مشاركة موسّعة وتوقيع 20 اتفاقية تعاون.",
                "Jeddah, Saudi Arabia — expanded participation and 20 cooperation agreements signed.")),
        new("assets/figma/milestones/card3-innovation.jpg",
            new("19–21 نوفمبر 2024", "19–21 November 2024"),
            new("الابتكار البحري", "Maritime innovation"),
            new("الظهران، المملكة العربية السعودية — منصة الابتكار واستراتيجية الاقتصاد الأزرق.",
                "Dhahran, Saudi Arabia — an innovation platform and blue-economy strategy.")),
        new("assets/figma/milestones/card1-future-startime.png",
            new("قريباً", "Soon"),
            new("المستقبل", "The future"),
            new("النسخة الأكبر والأكثر طموحاً — حدث سيادي رفيع المستوى يُنظّم تحت إشراف القوات البحرية، يرسّخ مكانة المملكة.",
                "The largest and most ambitious edition — a high-level sovereign event under the Royal Saudi Naval Forces, cementing the Kingdom's standing."),
            IsFuture: true),
    ];

    // ---- Themes / pillars ("المحاور الرئيسية") ---------------------------
    public sealed record Theme(Bilingual Title, Bilingual Desc);

    public static readonly IReadOnlyList<Theme> Themes =
    [
        new(new("التقنيات الحديثة وتأمين قاع البحار وسلاسل الإمداد", "Advanced technologies for securing the seabed and supply chains"),
            new("دور التقنيات الحديثة والابتكار في أمن قاع البحار وسلاسل الإمداد", "The role of modern technology and innovation in seabed and supply-chain security")),
        new(new("الحوكمة الدولية لأمن قاع البحار", "International governance of seabed security"),
            new("الجهود الدولية في حوكمة أمن وقاع البحار", "International efforts to govern seabed security")),
        new(new("أمن قاع البحار والأمن الدولي", "Seabed security and international security"),
            new("حماية قاع البحار وأثره على الأمن الدولي", "Protecting the seabed and its impact on international security")),
        new(new("تهديدات إمداد الطاقة وتداعياتها الاقتصادية", "Energy-supply threats and their economic impact"),
            new("التهديدات على سلاسل إمداد الطاقة وأثرها على الاقتصاد العالمي", "Threats to energy supply chains and their effect on the global economy")),
        new(new("البيئة الاستراتيجية العالمية وأمن سلاسل الإمداد البحرية", "The global strategic environment and maritime supply-chain security"),
            new("المتغيرات في البيئة الاستراتيجية العالمية وتأثيرها على أمن سلاسل الإمداد البحرية", "Shifts in the global strategic environment and their impact on maritime supply-chain security")),
    ];

    // ---- Main sessions ---------------------------------------------------
    public sealed record SessionCard(string Image, string Num, Bilingual Tag, Bilingual Title, Bilingual Text);

    public static readonly IReadOnlyList<SessionCard> Sessions =
    [
        new("assets/figma/sessions/session-card-1.jpg", "1",
            new("اليوم الأول", "Day One"),
            new("أمن سلاسل إمداد الطاقة البحرية", "Securing maritime energy supply chains"),
            new("تركيز على حماية منظومات الطاقة الممتدة عبر البحار، من خطوط وأنابيب النفط والغاز إلى البنية التحتية تحت السطح، في ظل تصاعد التهديدات الجيوسياسية وأهمية الممرات البحرية الحيوية.",
                "Protecting energy systems that stretch across the seas — from oil and gas pipelines to sub-surface infrastructure — amid rising geopolitical threats and the importance of vital maritime corridors.")),
        new("assets/figma/sessions/session-card-2.jpg", "2",
            new("اليوم الثاني", "Day Two"),
            new("سلاسل الإمداد البحرية والبنية التحتية اللوجستية", "Maritime supply chains and logistics infrastructure"),
            new("مستقبل التجارة البحرية ورفع مرونة سلاسل الإمداد وتعزيز كفاءة الموانئ والممرات البحرية عبر التقنيات الحديثة لضمان استدامة تدفق السلع عالمياً.",
                "The future of maritime trade — building supply-chain resilience and improving the efficiency of ports and sea lanes through modern technology to keep goods flowing worldwide.")),
        new("assets/figma/sessions/session-card-3.jpg", "3",
            new("اليوم الثالث", "Day Three"),
            new("أمن قاع البحار والبنية التحتية الرقمية", "Seabed security and digital infrastructure"),
            new("استعراض تحديات حماية الكابلات البحرية والأنظمة الرقمية في الأعماق، وتعزيز الأمن السيبراني البحري، ودور الدول في حماية الاقتصاد الرقمي العالمي المعتمد على البنية التحتية تحت سطح البحر.",
                "The challenges of protecting subsea cables and digital systems in the deep, strengthening maritime cybersecurity, and the role of nations in safeguarding the global digital economy that depends on undersea infrastructure.")),
    ];

    // ---- Partners band (government entities marquee) ---------------------
    public sealed record PartnerLogo(string Image, Bilingual Label);

    public static readonly IReadOnlyList<PartnerLogo> PartnerLogos =
    [
        new("assets/figma/partnersband/partner-1-amn-aldawla.png", new("رئاسة أمن الدولة", "Presidency of State Security")),
        new("assets/figma/partnersband/partner-2-istikhbarat.png", new("رئاسة الاستخبارات العامة", "General Intelligence Presidency")),
        new("assets/figma/partnersband/partner-3-haras-watani.png", new("وزارة الحرس الوطني", "Ministry of National Guard")),
        new("assets/figma/partnersband/partner-4-dakhiliya.png", new("وزارة الداخلية", "Ministry of Interior")),
    ];

    // ---- Sponsors (carousel) --------------------------------------------
    // Placeholder logos until real sponsor data is wired; modelled as a list
    // (like PartnerLogos) so the marquee stays data-driven — the view renders
    // it ×2 for the seamless -50% loop regardless of how many entries there are.
    public sealed record SponsorLogo(string Logo, Bilingual Tag);

    public static readonly IReadOnlyList<SponsorLogo> Sponsors =
        [.. Enumerable.Repeat(
            new SponsorLogo("assets/figma/sponsors/sponsor-1.svg", new("مستضيف", "Host")), 8)];

    // ---- News & articles -------------------------------------------------
    public sealed record NewsCard(string Image, string Date, Bilingual Title, Bilingual Excerpt);

    public static readonly IReadOnlyList<NewsCard> News =
    [
        new("assets/figma/news/news-1.jpg", "2026-10-16",
            new("جولة في أجنحة المعرض المصاحب", "A tour of the accompanying exhibition halls"),
            new("صور حصرية من داخل أجنحة أبرز الشركات المشاركة في المعرض 2026", "Exclusive photos from inside the pavilions of leading companies at Expo 2026")),
        new("assets/figma/news/news-2.jpg", "2026-10-15",
            new("كلمة افتتاحية حول مستقبل الأمن البحري", "An opening address on the future of maritime security"),
            new("تسجيل كامل للكلمة الافتتاحية في الجلسة الرئيسية الأولى 2026", "Full recording of the opening address in the first plenary session 2026")),
        new("assets/figma/news/news-3.jpg", "2026-10-15",
            new("انطلاق فعاليات الملتقى البحري السعودي", "The Saudi Maritime Forum kicks off"),
            new("لقطات من حفل الافتتاح الرسمي بحضور كبار المسؤولين والوفود الدولية", "Highlights from the official opening ceremony attended by senior officials and international delegations")),
    ];

    // ---- Discover Saudi Arabia ------------------------------------------
    // Distance = approx. driving distance from the Riyadh venue.
    public sealed record DiscoverCard(string Image, Bilingual Title, string Distance, Bilingual Location);

    public static readonly IReadOnlyList<DiscoverCard> DiscoverCards =
    [
        new("assets/figma/discover/discover-1.jpg", new("العُلا", "AlUla"), "1,100 km", new("منطقة المدينة المنورة", "Madinah Region")),
        new("assets/figma/discover/discover-2.jpg", new("الدرعية التاريخية", "Historic Diriyah"), "15 km", new("الرياض", "Riyadh")),
        new("assets/figma/discover/discover-3.jpg", new("جدة التاريخية", "Historic Jeddah"), "950 km", new("جدة", "Jeddah")),
        new("assets/figma/discover/discover-4.jpg", new("نيوم", "NEOM"), "1,500 km", new("تبوك", "Tabuk")),
        new("assets/figma/discover/discover-5.jpg", new("البحر الأحمر", "The Red Sea"), "900 km", new("تبوك", "Tabuk")),
        new("assets/figma/discover/discover-6.jpg", new("حافة العالم", "Edge of the World"), "90 km", new("الرياض", "Riyadh")),
    ];

}
