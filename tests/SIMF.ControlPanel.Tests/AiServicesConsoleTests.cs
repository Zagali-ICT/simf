// CP Phase-1 — behaviour test for the AI services console: it reads the prompt
// catalogue once and aggregates it into one row per AI service (feature),
// surfacing the active prompt's routing + the hybrid residency warning.
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class AiServicesConsoleTests : CpComponentTestBase
{
    private static AdminAiPromptSummary Prompt(
        AiFeature feature, AiProvider provider, string key, bool active, int version) =>
        new(Guid.NewGuid(), key, feature, key, key, provider, "model", 0.2, 512,
            active, version, DateTime.UnixEpoch, null);

    [Fact]
    public void Aggregates_prompts_into_one_row_per_service_and_flags_sensitive_on_cloud()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var items = new List<AdminAiPromptSummary>
        {
            // SessionSummary has two prompts — the active one (Gemini) drives the row.
            Prompt(AiFeature.SessionSummary, AiProvider.Gemini, "session-summary", true, 2),
            Prompt(AiFeature.SessionSummary, AiProvider.Echo, "session-summary-old", false, 1),
            Prompt(AiFeature.Faq, AiProvider.Echo, "faq-answer", true, 1),
        };
        var page = GridPage<AdminAiPromptSummary>.Of(items, items.Count, new GridQuery { Top = 500 });
        JSInterop.Setup<ApiResult<GridPage<AdminAiPromptSummary>>>(
            inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminAiPromptSummary>>.Ok(page));

        var cut = RenderComponent<AiServicesConsole>();

        cut.WaitForAssertion(() =>
        {
            // The active prompt (not the inactive older snapshot) drives the service row.
            Assert.Contains("session-summary", cut.Markup);
            Assert.Contains("Gemini", cut.Markup);
            Assert.Contains("faq-answer", cut.Markup);
            // SessionSummary (sensitive) on Gemini (cloud) raises the residency warning;
            // CpComponentTestBase's localizer renders the resource key verbatim.
            Assert.Contains("Admin.AiPrompts.Hosting.Risk", cut.Markup);
        });
    }

    // The "Configure routing" action is gated by AuthorizedAction (AiPrompts.Edit);
    // bUnit's test principal carries the Administrator role but not the per-action
    // permission claim, so the gated button + its GET/PUT modal flow are covered by
    // the E2E catalogue (cp-admin-ai-services.md, E2E-AIS-012..014) rather than here.
    // The underlying prompt PUT is already unit-covered by AiPromptsAddEditTests.
}
