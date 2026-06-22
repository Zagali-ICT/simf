// Tests: SIMF.ControlPanel.Tests/AiServicesConsoleTests.cs
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>Shared CP-side hosting/residency classification for the AI pages
/// (the prompt catalogue + the services console). Purely derived from the
/// provider + feature a row already carries — no schema, no persisted state.
///
/// <para>OpenAI's cloud-vs-on-prem status depends on its configured
/// <c>BaseUrl</c> (global config the row does not carry), so it is deliberately
/// shown as endpoint-dependent and never flagged as a residency risk: under the
/// hybrid policy it is typically the on-prem endpoint. Only definitely-external
/// SaaS providers are "Cloud" and can raise the residency warning.</para></summary>
internal static class AiHosting
{
    /// <summary>Providers that always reach an external SaaS endpoint.</summary>
    public static bool IsDefinitelyCloud(AiProvider provider) =>
        provider is AiProvider.Gemini or AiProvider.Anthropic or AiProvider.AzureOpenAi;

    /// <summary>Features that process actual session / defense content — keeping
    /// these on a cloud provider is the data-residency concern the warning
    /// surfaces (NCA/DoD).</summary>
    private static readonly HashSet<AiFeature> SensitiveFeatures = new()
    {
        AiFeature.SessionSummary,
        AiFeature.Assistance,
        AiFeature.LiveTranslation,
        AiFeature.LiveSignLanguage,
    };

    /// <summary>A sensitive feature routed to a definitely-cloud provider.</summary>
    public static bool IsResidencyRisk(AiFeature feature, AiProvider provider) =>
        SensitiveFeatures.Contains(feature) && IsDefinitelyCloud(provider);

    /// <summary>The <see cref="SIMF.Components.Forms.SimfPill"/> variant for the
    /// hosting badge.</summary>
    public static string Variant(AiProvider provider) => provider switch
    {
        AiProvider.Echo => "off",
        AiProvider.OpenAi => "admin",
        _ => "warn",
    };

    /// <summary>The resx key for the hosting label (resolved by the page's own
    /// localizer so the helper stays UI-framework-free).</summary>
    public static string LabelKey(AiProvider provider) => provider switch
    {
        AiProvider.Echo => "Admin.AiPrompts.Hosting.Offline",
        AiProvider.OpenAi => "Admin.AiPrompts.Hosting.OpenAi",
        _ => "Admin.AiPrompts.Hosting.Cloud",
    };
}
