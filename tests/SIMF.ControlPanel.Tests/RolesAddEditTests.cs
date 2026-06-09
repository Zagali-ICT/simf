// D-353 rollout â€” behaviour tests for RolesAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs, Edit PUTs to the row id, and a
// built-in (baseline) role in Edit mode shows the read-only notice instead of
// the rename field (mirrors the old RolesList edit-modal guard).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class RolesAddEditTests : CpComponentTestBase
{
    private static AdminRoleSummary Custom() =>
        new(Guid.NewGuid(), "Reviewers", IsBaseline: false, UserCount: 3, PermissionCount: 5);

    private static AdminRoleSummary Baseline() =>
        new(Guid.NewGuid(), "Administrator", IsBaseline: true, UserCount: 1, PermissionCount: 99);

    [Fact]
    public void Add_mode_shows_the_name_field_and_create_submit()
    {
        var cut = RenderComponent<RolesAddEdit>(p => p.Add(x => x.IsEdit, false));

        Assert.NotEmpty(cut.FindAll("input.simf-field__input"));
        Assert.Contains("Admin.Roles.New.Submit", cut.Markup);
    }

    [Fact]
    public void Edit_mode_on_a_baseline_role_shows_the_notice_and_no_form()
    {
        var cut = RenderComponent<RolesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Baseline()));

        Assert.Contains("Admin.Roles.Edit.BaselineNotice", cut.Markup);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void Edit_mode_on_a_custom_role_shows_the_rename_form()
    {
        var cut = RenderComponent<RolesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Custom()));

        Assert.NotEmpty(cut.FindAll("form"));
        Assert.Contains("Admin.Roles.Edit.Submit", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminRoleSummary(Guid.NewGuid(), "Auditors", false, 0, 0);
        var handler = JSInterop.Setup<ApiResult<AdminRoleSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminRoleSummary>.Ok(created));

        var cut = RenderComponent<RolesAddEdit>(p => p.Add(x => x.IsEdit, false));

        cut.Find("input.simf-field__input").Change("Auditors");
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/roles", (string)posted.Arguments[0]!);
        var body = (AdminCreateRoleRequest)posted.Arguments[1]!;
        Assert.Equal("Auditors", body.Name);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Custom();
        var handler = JSInterop.Setup<ApiResult<AdminRoleSummary>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminRoleSummary>.Ok(row));

        var cut = RenderComponent<RolesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/roles/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateRoleRequest>(put.Arguments[1]);
    }
}
