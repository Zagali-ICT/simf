// D-353 rollout — behaviour tests for AiPromptsAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Add POSTs a CreateAiPromptRequest and hides the
// Active checkbox; Edit PUTs an UpdateAiPromptRequest to the row id and shows
// the Active checkbox. Regression guard for the prior CS0542 (the nested form
// model is FormModel, not Model, so its Model property compiles).
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class AiPromptsAddEditTests : CpComponentTestBase
{
    private static AdminAiPromptDetail Detail() => new(
        Guid.NewGuid(),
        "question-filter",
        AiFeature.QuestionFilter,
        "Question filter",
        "مرشّح الأسئلة",
        Description: null,
        DescriptionArabic: null,
        AiProvider.Echo,
        "echo",
        "You are a filter.",
        "Filter: {text}",
        Temperature: 0.2,
        MaxOutputTokens: 512,
        IsActive: true,
        Version: 1,
        DateTime.UnixEpoch,
        UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<AiPromptsAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.AiPrompts.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<AiPromptsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.AiPrompts.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminAiPromptDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminAiPromptDetail>.Ok(created));

        var cut = RenderComponent<AiPromptsAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change — @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("welcome-prompt");  // Key
        cut.FindAll("input.simf-field__input")[1].Change("Welcome");         // DisplayName
        cut.FindAll("input.simf-field__input")[2].Change("ترحيب");           // DisplayNameArabic
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/ai/prompts", (string)posted.Arguments[0]!);
        var body = (CreateAiPromptRequest)posted.Arguments[1]!;
        Assert.Equal("welcome-prompt", body.Key);
        Assert.Equal("Welcome", body.DisplayName);
        Assert.Equal("ترحيب", body.DisplayNameArabic);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminAiPromptDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminAiPromptDetail>.Ok(row));

        var cut = RenderComponent<AiPromptsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/ai/prompts/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<UpdateAiPromptRequest>(put.Arguments[1]);
    }
}
