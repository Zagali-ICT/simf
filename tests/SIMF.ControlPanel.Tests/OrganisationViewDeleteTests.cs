// D-353 rollout â€” behaviour tests for OrganisationViewDelete (merged
// View/Delete form). View shows details only; Delete mode gates the soft-delete
// behind SimfConfirm (no DELETE until confirmed).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Organisations;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class OrganisationViewDeleteTests : CpComponentTestBase
{
    public OrganisationViewDeleteTests()
    {
        // D-833 — the confirm button is gated on the code the endpoint behind
        // it needs; this test drives that button, so the identity holds it.
        Grant(PermissionCatalog.Organisations.Delete);
    }

    private static AdminOrganisationDetail Detail() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Ø´Ø±ÙƒØ© Ø§Ù„Ø§Ø®ØªØ¨Ø§Ø±",
        "Test Co",
        "1010101010",
        "Maritime",
        "Riyadh",
        "+966500000000",
        "info@test.example",
        "https://test.example",
        IsActive: true,
        DateTime.UnixEpoch,
        UpdatedAt: null);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        var cut = RenderComponent<OrganisationViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, Detail()));

        Assert.Contains("Test Co", cut.Markup);
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

        var row = Detail();
        var cut = RenderComponent<OrganisationViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));      // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);          // nothing deleted yet

        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/organisations/{row.Id}", (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<OrganisationViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, Detail()));

        cut.Find(".simf-button--danger").Click();
        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.Empty(deleteHandler.Invocations);
    }
}
