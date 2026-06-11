// D-353 rollout (batch 1) — behaviour tests for CountryAddEdit (merged Add/Edit
// form). Pins the IsEdit branch: Create POSTs (with the ISO numeric Id), Edit
// PUTs to the row id, and the Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class CountryAddEditTests : CpComponentTestBase
{
    private static AdminCountryDetail Detail() => new(
        682, "SA", "Saudi Arabia", "المملكة العربية السعودية", "+966", 1,
        IsActive: true, DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<CountryAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Countries.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<CountryAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Countries.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request_with_the_iso_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminCountryDetail(
            900, "ZZ", "Testland", "أرض الاختبار", null, 0, true, DateTimeOffset.UnixEpoch, null);
        var handler = JSInterop.Setup<ApiResult<AdminCountryDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminCountryDetail>.Ok(created));

        var cut = RenderComponent<CountryAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change — @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("900");          // ISO numeric Id
        cut.FindAll("input.simf-field__input")[1].Change("ZZ");           // 2-letter code
        cut.FindAll("input.simf-field__input")[2].Change("Testland");
        cut.FindAll("input.simf-field__input")[3].Change("أرض الاختبار");
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/countries", (string)posted.Arguments[0]!);
        var body = (AdminCreateCountryRequest)posted.Arguments[1]!;
        Assert.Equal(900, body.Id);
        Assert.Equal("ZZ", body.Code);
        Assert.Equal("Testland", body.Name);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminCountryDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminCountryDetail>.Ok(row));

        var cut = RenderComponent<CountryAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/countries/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateCountryRequest>(put.Arguments[1]);
    }
}
