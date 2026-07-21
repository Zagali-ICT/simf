using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public "Objectives" page (/about/objectives — Figma
// 5865-34626). The second About-cluster page: a static list of the forum's six
// strategic objectives, rendered as the shared ln-fcard feature cards on the
// ln-fsection layout. Section headers come from resx (Objectives.* keys); the
// card content lives here as Bilingual so one model renders in either language.
public partial class Objectives
{
    // A single objective feature card: icon asset + bilingual title + description.
    public sealed record Objective(string Icon, Bilingual Title, Bilingual Desc);

    // The six strategic objectives ("ستة أهداف رئيسية"), in Figma order.
    public static readonly IReadOnlyList<Objective> ObjectivesList =
    [
        new("assets/figma/objectives/icon-security.svg",
            new("تعزيز الأمن البحري", "Strengthening maritime security"),
            new("بناء منظومات حماية متكاملة للموانئ والسفن والممرات.",
                "Building integrated protection systems for ports, ships and corridors.")),
        new("assets/figma/objectives/icon-supply.svg",
            new("مرونة سلاسل الإمداد", "Supply-chain resilience"),
            new("ضمان استمرارية التجارة العالمية عبر الممرات الحيوية.",
                "Ensuring the continuity of global trade through vital corridors.")),
        new("assets/figma/objectives/icon-energy.svg",
            new("أمن الطاقة", "Energy security"),
            new("تأمين تدفق إمدادات الطاقة عبر المسارات البحرية.",
                "Securing the flow of energy supplies across maritime routes.")),
        new("assets/figma/objectives/icon-infrastructure.svg",
            new("حماية البنية التحتية", "Protecting infrastructure"),
            new("تأمين الكابلات والأنابيب والبنية تحت سطح البحر.",
                "Securing cables, pipelines and sub-sea infrastructure.")),
        new("assets/figma/objectives/icon-digital.svg",
            new("التحول الرقمي", "Digital transformation"),
            new("تبنّي التقنيات الحديثة لوعي بحري لحظي.",
                "Adopting modern technologies for real-time maritime awareness.")),
        new("assets/figma/objectives/icon-cooperation.svg",
            new("التعاون الدولي", "International cooperation"),
            new("تعزيز التنسيق وتبادل المعلومات بين الشركاء.",
                "Strengthening coordination and information-sharing among partners.")),
    ];
}
