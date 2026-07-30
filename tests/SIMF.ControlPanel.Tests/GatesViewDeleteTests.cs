// D-353 rollout â€” behaviour tests for GatesViewDelete (merged View/Delete
// form). View shows details only; Delete mode gates the soft-delete behind
// SimfConfirm (no DELETE until confirmed); cancelling the confirm fires no
// delete.
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class GatesViewDeleteTests : CpComponentTestBase
{
    private static AdminGateDetail Detail() => new(
        Guid.NewGuid(), "GATE-1", "North Gate", "Ø§Ù„Ø¨ÙˆØ§Ø¨Ø© Ø§Ù„Ø´Ù…Ø§Ù„ÙŠØ©",
        Description: "Main entrance", DescriptionArabic: "Ø§Ù„Ù…Ø¯Ø®Ù„ Ø§Ù„Ø±Ø¦ÙŠØ³ÙŠ",
        DirectionMode.Both, IsActive: true,
        AllowedProfileTypeIds: Array.Empty<Guid>(),
        AssignedOperatorUserIds: Array.Empty<Guid>(),
        DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        // BUG-018 — the form now loads the gate's assignments on init.
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<GatesViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, Detail()));

        Assert.Contains("North Gate", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-button--danger"));
        Assert.Empty(cut.FindAll(".simf-modal"));
    }

    // BUG-018 (18-6) — the detail view used to render only the operator COUNT, so
    // an assignment could not be audited from the CP. It must name each operator.
    [Fact]
    public void View_mode_lists_the_assigned_operators_by_name_and_email()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminGateAssignmentRow>>>(
            "simfAccount.getJson",
            $"/account/api/admin/gates/{row.Id}/assignments")
            .SetResult(ApiResult<IReadOnlyList<AdminGateAssignmentRow>>.Ok(
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Ahmed Al-Rashid",
                    DateTimeOffset.UnixEpoch, Guid.NewGuid(), "ahmed@simf.test"),
            ]));

        var cut = RenderComponent<GatesViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, row));

        Assert.Contains("Ahmed Al-Rashid", cut.Markup);
        Assert.Contains("ahmed@simf.test", cut.Markup);
    }

    [Fact]
    public void Delete_mode_gates_the_call_behind_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var row = Detail();
        var cut = RenderComponent<GatesViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));      // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);          // nothing deleted yet

        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/gates/{row.Id}", (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<GatesViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, Detail()));

        cut.Find(".simf-button--danger").Click();
        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.Empty(deleteHandler.Invocations);
    }
}
