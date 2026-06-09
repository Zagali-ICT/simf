// D-353 — behaviour tests for InterestViewDelete (the merged View/Delete form).
// Pins: View mode shows details only; Delete mode adds the Deactivate button;
// the destructive call is gated behind SimfConfirm (no DELETE until confirmed).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class InterestViewDeleteTests : CpComponentTestBase
{
    private static AdminInterestSummary Row() => new(
        Guid.NewGuid(), "Naval Engineering", "الهندسة البحرية", 10, IsActive: true, DateTimeOffset.UnixEpoch);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        var row = Row();
        var cut = RenderComponent<InterestViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, false)
            .Add(p => p.Initial, row));

        Assert.Contains("Naval Engineering", cut.Markup);
        // No destructive (Deactivate) button in read-only View mode.
        Assert.Empty(cut.FindAll(".simf-button--danger"));
        // No confirmation dialog open.
        Assert.Empty(cut.FindAll(".simf-modal"));
    }

    [Fact]
    public void Delete_mode_gates_the_call_behind_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var row = Row();
        var cut = RenderComponent<InterestViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, true)
            .Add(p => p.Initial, row));

        // The Deactivate button shows, but no confirm dialog and no DELETE yet.
        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));            // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);                // nothing deleted yet

        // Confirm inside the dialog fires exactly one DELETE to the row id.
        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/interests/{row.Id}", (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<InterestViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, true)
            .Add(p => p.Initial, Row()));

        cut.Find(".simf-button--danger").Click();                       // open confirm
        cut.Find(".simf-modal__footer .simf-button--secondary").Click(); // cancel

        Assert.Empty(deleteHandler.Invocations);
    }
}
