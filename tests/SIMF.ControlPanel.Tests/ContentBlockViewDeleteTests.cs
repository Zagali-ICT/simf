// D-353 rollout â€” behaviour tests for ContentBlockViewDelete (merged View/Delete
// form). View shows details only; Delete mode gates the soft-delete-by-Key
// behind SimfConfirm (no DELETE until confirmed). The DELETE route keys off the
// (URL-escaped) Key, not an id.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ContentBlockViewDeleteTests : CpComponentTestBase
{
    private static AdminContentBlockSummary Summary() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "home.welcome.title", "Welcome", "Ø£Ù‡Ù„Ø§Ù‹",
        IsActive: true, DateTime.UnixEpoch, Guid.Empty);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        var cut = RenderComponent<ContentBlockViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, Summary()));

        Assert.Contains("home.welcome.title", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-button--danger"));
        Assert.Empty(cut.FindAll(".simf-modal"));
    }

    [Fact]
    public void Delete_mode_gates_the_call_behind_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var row = Summary();
        var cut = RenderComponent<ContentBlockViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));      // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);          // nothing deleted yet

        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal(
            $"/account/api/admin/content-blocks/{Uri.EscapeDataString(row.Key)}",
            (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<ContentBlockViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, Summary()));

        cut.Find(".simf-button--danger").Click();
        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.Empty(deleteHandler.Invocations);
    }
}
