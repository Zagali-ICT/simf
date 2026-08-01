// D-353 rollout â€” behaviour tests for OrganisationAddEdit (merged Add/Edit
// form). Pins the IsEdit branch: Create POSTs a CreateOrganisationRequest, Edit
// PUTs an UpdateOrganisationRequest to the row id, and the Active checkbox only
// appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Organisations;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class OrganisationAddEditTests : CpComponentTestBase
{
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
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<OrganisationAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Organisations.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<OrganisationAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Organisations.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminOrganisationDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminOrganisationDetail>.Ok(created));

        var cut = RenderComponent<OrganisationAddEdit>(p => p.Add(x => x.IsEdit, false));

        // First text field is the Arabic name (the only required field).
        cut.FindAll("input.simf-field__input")[0].Change("Ø´Ø±ÙƒØ© Ø¬Ø¯ÙŠØ¯Ø©");
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/organisations", (string)posted.Arguments[0]!);
        var body = (CreateOrganisationRequest)posted.Arguments[1]!;
        Assert.Equal("Ø´Ø±ÙƒØ© Ø¬Ø¯ÙŠØ¯Ø©", body.NameAr);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminOrganisationDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminOrganisationDetail>.Ok(row));

        var cut = RenderComponent<OrganisationAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/organisations/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<UpdateOrganisationRequest>(put.Arguments[1]);
    }

    [Fact]
    public void Add_mode_blocks_submit_when_arabic_name_is_blank()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var handler = JSInterop.Setup<ApiResult<AdminOrganisationDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminOrganisationDetail>.Ok(Detail()));

        var cut = RenderComponent<OrganisationAddEdit>(p => p.Add(x => x.IsEdit, false));
        cut.Find("form").Submit();

        Assert.Empty(handler.Invocations);
        Assert.Contains("Admin.Organisations.Required", cut.Markup);
    }
}
