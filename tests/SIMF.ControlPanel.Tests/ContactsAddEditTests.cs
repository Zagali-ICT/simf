// D-353 — behavior tests for the ContactsAddEdit form (the reusable Add/Edit
// component hosted by CrudShell from ContactsList). Pins the IsEdit branch:
//   • IsEdit=false → POST /account/api/admin/contacts (Create);
//   • IsEdit=true  → PUT  /account/api/admin/contacts/{id} (Update) and the
//     IsActive checkbox is rendered.
// The form also calls simfAccount.postJson against /countries/list on first
// render to populate the country picker, so the assertions filter the captured
// invocations by URL rather than asserting a single call.
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Contracts.Contacts;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ContactsAddEditTests : CpComponentTestBase
{
    private static AdminContactDetail SampleDetail() => new(
        Id: Guid.NewGuid(),
        NameAr: "جهة",
        NameEn: "Acme",
        LogoRelativePath: "logos/acme.png",
        PhonePrimary: "+966500000000",
        PhoneSecondary: null,
        Email: "info@acme.test",
        Website: "https://acme.test",
        FacebookUrl: null,
        XUrl: null,
        LinkedInUrl: null,
        InstagramUrl: null,
        Latitude: 24.7136,
        Longitude: 46.6753,
        CountryId: 682,
        CountryNameEn: "Saudi Arabia",
        CountryNameAr: "السعودية",
        IsActive: true,
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_IsActive_checkbox()
    {
        // IsEdit=false (Add): the IsActive flag is implicit (active) on create,
        // so the checkbox is only shown in Edit mode.
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<ContactsAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // The IsActive checkbox (a .simf-checkbox__label span) is absent in Add.
        Assert.Empty(cut.FindAll(".simf-checkbox__label"));
    }

    [Fact]
    public void Edit_mode_shows_the_IsActive_checkbox()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<ContactsAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, true)
            .Add(p => p.Initial, SampleDetail()));

        // The IsActive checkbox renders its label in a .simf-checkbox__label span.
        var checkboxLabels = cut.FindAll(".simf-checkbox__label").Select(e => e.TextContent.Trim());
        Assert.Contains("Admin.Contacts.Field.IsActive", checkboxLabels);
    }

    [Fact]
    public void Add_mode_posts_create_to_the_contacts_collection()
    {
        // IsEdit=false → the submit path is a POST to the collection route.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = SampleDetail();
        var saveHandler = JSInterop.Setup<ApiResult<AdminContactDetail>>(
            inv => inv.Identifier == "simfAccount.postJson"
                   && (string)inv.Arguments[0]! == "/account/api/admin/contacts")
            .SetResult(ApiResult<AdminContactDetail>.Ok(created));

        var cut = RenderComponent<ContactsAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Arabic name is the only required field — fill it then submit.
        cut.FindAll("input.simf-field__input")[0].Change("جهة جديدة");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(saveHandler.Invocations));
        var posted = saveHandler.Invocations.Single();
        Assert.Equal("/account/api/admin/contacts", (string)posted.Arguments[0]!);
        var body = (CreateContactRequest)posted.Arguments[1]!;
        Assert.Equal("جهة جديدة", body.NameAr);
    }

    [Fact]
    public void Edit_mode_puts_update_to_the_contact_id_route()
    {
        // IsEdit=true → the submit path is a PUT to /contacts/{id}, carrying the
        // IsActive flag (only the Edit branch sends it).
        JSInterop.Mode = JSRuntimeMode.Loose;
        var detail = SampleDetail();
        var saveHandler = JSInterop.Setup<ApiResult<AdminContactDetail>>(
            inv => inv.Identifier == "simfAccount.putJson")
            .SetResult(ApiResult<AdminContactDetail>.Ok(detail));

        var cut = RenderComponent<ContactsAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, true)
            .Add(p => p.Initial, detail));

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Single(saveHandler.Invocations));
        var put = saveHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/contacts/{detail.Id}", (string)put.Arguments[0]!);
        var body = (UpdateContactRequest)put.Arguments[1]!;
        Assert.Equal(detail.NameAr, body.NameAr);
        Assert.True(body.IsActive);
    }

    [Fact]
    public void Empty_arabic_name_blocks_submit_and_shows_required_error()
    {
        // The form short-circuits an obviously-empty submit (Arabic name blank)
        // before any network call, surfacing the Required validation message.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var saveHandler = JSInterop.Setup<ApiResult<AdminContactDetail>>(
            inv => inv.Identifier == "simfAccount.postJson"
                   && (string)inv.Arguments[0]! == "/account/api/admin/contacts")
            .SetResult(ApiResult<AdminContactDetail>.Ok(SampleDetail()));

        var cut = RenderComponent<ContactsAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Submit with the Arabic name still empty.
        cut.Find("form").Submit();

        Assert.Empty(saveHandler.Invocations);
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Contacts.Required", cut.Markup));
    }
}
