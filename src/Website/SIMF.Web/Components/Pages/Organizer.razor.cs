using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public "The organizer" page (/about/organizer — Figma
// 5865-38003). The fourth About-cluster page: the forum's organising bodies as
// two cards. Section headers come from resx (Organizer.* keys); the card content
// lives here as Bilingual so one model renders in either language.
//
// The Figma frame ships placeholder content (a dev-company logo, duplicated body
// text). This fills the shell with the REAL bodies — Ministry of Defense (patron)
// + Royal Saudi Naval Forces (organiser), per the forum's "MOD.RSNF" identity.
// The RSNF card recolours the forum mark to navy (LogoMasked) as a stand-in until
// the RSNF emblem asset is supplied (see web/organizer.md §7).
public partial class Organizer
{
    // An organising body: its logo (LogoMasked = paint the forum mark navy via a
    // CSS mask, for the white-on-white forum logo), bilingual name + description.
    public sealed record OrgBody(string Logo, bool LogoMasked, Bilingual Name, Bilingual Desc);

    public static readonly IReadOnlyList<OrgBody> Organizers =
    [
        new("assets/figma/organizer/mod-emblem.svg", false,
            new("وزارة الدفاع — المملكة العربية السعودية", "Ministry of Defense — Saudi Arabia"),
            new("ينعقد الملتقى البحري السعودي الدولي الرابع تحت رعاية كريمة من وزارة الدفاع، ليكون منصة رسمية تعكس التزام المملكة بتعزيز الأمن والتعاون البحري الدولي.",
                "The 4th Saudi International Maritime Forum is held under the gracious patronage of the Ministry of Defense — an official platform reflecting the Kingdom's commitment to advancing maritime security and international cooperation.")),
        new("assets/figma/nav/logo-fill.svg", true,
            new("القوات البحرية الملكية السعودية", "Royal Saudi Naval Forces"),
            new("تنظّم القوات البحرية الملكية السعودية الملتقى بالتعاون مع الجهات الحكومية ذات الصلة، جامعةً القادة والخبراء الدوليين حول مستقبل الأمن البحري وحماية الممرات الملاحية.",
                "The Royal Saudi Naval Forces organise the forum in cooperation with relevant government bodies, convening international leaders and experts around the future of maritime security and the protection of shipping lanes.")),
    ];
}
