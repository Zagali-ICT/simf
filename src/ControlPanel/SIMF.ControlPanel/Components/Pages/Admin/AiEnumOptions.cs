// Tests: SIMF.ControlPanel.Tests/AiPromptsAddEditTests.cs, AiServicesConsoleTests.cs
using System.Globalization;
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>Shared <c>SimfSelect&lt;string&gt;</c> option helpers for the AI
/// <see cref="AiFeature"/> / <see cref="AiProvider"/> enums (used by the prompt
/// Add/Edit form and the services-console routing editor). The selects bind the
/// enum as its int-string id and parse it back at submit — <c>SimfSelect</c>
/// projects a <c>TValue?</c> into <c>ValueFor</c>, which a value-type enum
/// cannot satisfy, so the string-id indirection is the established pattern
/// (see SessionsAddEdit). Centralised here so the two pages do not drift.</summary>
internal static class AiEnumOptions
{
    public static readonly string[] Providers = Enum.GetValues<AiProvider>()
        .Select(p => ((int)p).ToString(CultureInfo.InvariantCulture)).ToArray();

    public static readonly string[] Features = Enum.GetValues<AiFeature>()
        .Select(f => ((int)f).ToString(CultureInfo.InvariantCulture)).ToArray();

    public static string Id(AiProvider provider) =>
        ((int)provider).ToString(CultureInfo.InvariantCulture);

    public static string Id(AiFeature feature) =>
        ((int)feature).ToString(CultureInfo.InvariantCulture);

    public static string ProviderLabel(string id) =>
        int.TryParse(id, out var n) && Enum.IsDefined(typeof(AiProvider), n)
            ? ((AiProvider)n).ToString() : id;

    public static string FeatureLabel(string id) =>
        int.TryParse(id, out var n) && Enum.IsDefined(typeof(AiFeature), n)
            ? ((AiFeature)n).ToString() : id;

    public static AiProvider ParseProvider(string id) =>
        int.TryParse(id, out var n) && Enum.IsDefined(typeof(AiProvider), n)
            ? (AiProvider)n : AiProvider.Echo;

    public static AiFeature ParseFeature(string id) =>
        int.TryParse(id, out var n) && Enum.IsDefined(typeof(AiFeature), n)
            ? (AiFeature)n : AiFeature.QuestionFilter;

    /// <summary>Format a 0..1 rate as a one-decimal percentage (e.g. "5.0%").
    /// Shared by the AI dashboard + service-detail analytics so the percent
    /// policy can't drift between them.</summary>
    public static string Pct(double rate) =>
        (rate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
