// CP Phase-2 — behaviour test for the tabbed AI service-detail page: an unknown
// route param shows the unknown state, and a known service lists only ITS prompts
// on the Prompts tab.
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class AiServiceDetailTests : CpComponentTestBase
{
    private static AdminAiPromptSummary Prompt(AiFeature feature, string key, AiProvider provider) =>
        new(Guid.NewGuid(), key, feature, key, key, provider, "model", 0.2, 512,
            true, 1, DateTimeOffset.UnixEpoch, null);

    [Fact]
    public void An_unknown_feature_shows_the_unknown_state()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<AiServiceDetail>(p => p.Add(x => x.Feature, "NotAFeature"));
        Assert.Contains("Admin.AiServiceDetail.Unknown", cut.Markup);
    }

    [Fact]
    public void A_known_service_lists_only_its_own_prompts_on_the_prompts_tab()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var items = new List<AdminAiPromptSummary>
        {
            Prompt(AiFeature.SessionSummary, "session-summary", AiProvider.Gemini),
            Prompt(AiFeature.Faq, "faq-answer", AiProvider.Echo),
        };
        var page = GridPage<AdminAiPromptSummary>.Of(items, items.Count, new GridQuery { Top = 500 });
        JSInterop.Setup<ApiResult<GridPage<AdminAiPromptSummary>>>(
            inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminAiPromptSummary>>.Ok(page));

        var cut = RenderComponent<AiServiceDetail>(p => p.Add(x => x.Feature, "SessionSummary"));

        // The tab strip renders; switch to the Prompts tab.
        cut.WaitForAssertion(() => Assert.Contains("Admin.AiServiceDetail.Tab.Routing", cut.Markup));
        var promptsTab = cut.FindAll("button.simf-tabs__tab")
            .Single(b => b.TextContent.Contains("Admin.AiServices.Col.Prompts"));
        promptsTab.Click();

        // Only the SessionSummary prompt is listed — the FAQ prompt is filtered out.
        Assert.Contains("session-summary", cut.Markup);
        Assert.DoesNotContain("faq-answer", cut.Markup);
    }
}
