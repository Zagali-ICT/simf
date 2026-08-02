// D-353 rollout — behaviour tests for ConfigurationAddEdit (merged Add/Edit
// form for System Configuration). Pins the IsEdit branch: Create POSTs the
// key+value, Edit PUTs to the row id (no key in the update body), and the
// Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ConfigurationAddEditTests : CpComponentTestBase
{
    private static AdminSystemSettingDetail Detail() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "registration.open", "true", "Whether registration is open",
        IsActive: true, DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<ConfigurationAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Configuration.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<ConfigurationAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Configuration.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request_with_the_key()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminSystemSettingDetail(
            Guid.NewGuid(), "new.key", "v1", "desc", true, DateTime.UnixEpoch, null);
        var handler = JSInterop.Setup<ApiResult<AdminSystemSettingDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminSystemSettingDetail>.Ok(created));

        var cut = RenderComponent<ConfigurationAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change — @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("new.key");      // Key
        cut.FindAll("input.simf-field__input")[1].Change("v1");           // Value
        cut.FindAll("input.simf-field__input")[2].Change("desc");         // Description
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/system-settings", (string)posted.Arguments[0]!);
        var body = (AdminCreateSystemSettingRequest)posted.Arguments[1]!;
        Assert.Equal("new.key", body.Key);
        Assert.Equal("v1", body.Value);
        Assert.Equal("desc", body.Description);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminSystemSettingDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminSystemSettingDetail>.Ok(row));

        var cut = RenderComponent<ConfigurationAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/system-settings/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateSystemSettingRequest>(put.Arguments[1]);
    }
}
