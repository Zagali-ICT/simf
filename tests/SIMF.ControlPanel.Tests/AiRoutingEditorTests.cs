// CP Phase-2 — behaviour test for the shared AiRoutingEditor: it loads the
// prompt (GET), and on save PUTs the whole prompt back with only the routing
// fields changed, then raises OnSaved.
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class AiRoutingEditorTests : CpComponentTestBase
{
    private static AdminAiPromptDetail Detail(Guid id) => new(
        id, "session-summary", AiFeature.SessionSummary,
        "Session summary", "ملخص الجلسة",
        Description: null, DescriptionArabic: null,
        AiProvider.Echo, "echo",
        "You are the rapporteur.", "Session: {sessionTitle}",
        Temperature: 0.2, MaxOutputTokens: 512,
        IsActive: true, Version: 1, DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Loads_the_prompt_then_saves_only_the_routing_change()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var id = Guid.NewGuid();
        var detail = Detail(id);
        JSInterop.Setup<ApiResult<AdminAiPromptDetail>>(
            inv => inv.Identifier == "simfAccount.getJson")
            .SetResult(ApiResult<AdminAiPromptDetail>.Ok(detail));
        var putHandler = JSInterop.Setup<ApiResult<AdminAiPromptDetail>>(
            inv => inv.Identifier == "simfAccount.putJson")
            .SetResult(ApiResult<AdminAiPromptDetail>.Ok(detail));
        var saved = false;

        var cut = RenderComponent<AiRoutingEditor>(p => p
            .Add(x => x.PromptId, id)
            .Add(x => x.OnSaved, () => saved = true));

        // The loaded prompt key renders once the GET resolves.
        cut.WaitForAssertion(() => Assert.Contains("session-summary", cut.Markup));

        // Retarget the service to Gemini (the provider select holds int-string ids).
        cut.Find("select.simf-field__select").Change(((int)AiProvider.Gemini).ToString());
        cut.Find("button").Click(); // the Save button (no Cancel bound)

        var put = putHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/ai/prompts/{id}", (string)put.Arguments[0]!);
        var body = (UpdateAiPromptRequest)put.Arguments[1]!;
        Assert.Equal(AiProvider.Gemini, body.Provider);
        // The non-routing fields are echoed unchanged from the loaded detail.
        Assert.Equal(AiFeature.SessionSummary, body.Feature);
        Assert.Equal("You are the rapporteur.", body.SystemPrompt);
        Assert.True(saved);
    }
}
