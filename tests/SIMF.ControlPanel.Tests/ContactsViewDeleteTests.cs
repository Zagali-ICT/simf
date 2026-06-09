// D-353 — behavior tests for the ContactsViewDelete form (the reusable
// View/Delete component hosted by CrudShell from ContactsList). Pins:
//   • IsDelete=false (View) → read-only details, no Deactivate button;
//   • IsDelete=true  (Delete) → Deactivate button opens a SimfConfirm, and
//     confirming calls DELETE /account/api/admin/contacts/{id};
//   • a server failure (e.g. 409 CONTACT_IN_USE) closes the confirm and
//     surfaces the error on the visible form body.
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Contracts.Contacts;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ContactsViewDeleteTests : CpComponentTestBase
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
    public void View_mode_omits_the_Deactivate_action()
    {
        // IsDelete=false (View): read-only details only — no destructive action.
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<ContactsViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, false)
            .Add(p => p.Initial, SampleDetail()));

        Assert.DoesNotContain("Admin.Contacts.Action.Deactivate", cut.Markup);
        // The read-only detail list is rendered.
        Assert.NotNull(cut.Find("dl.simf-dl"));
    }

    [Fact]
    public void Delete_mode_shows_the_Deactivate_action()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<ContactsViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, true)
            .Add(p => p.Initial, SampleDetail()));

        Assert.Contains("Admin.Contacts.Action.Deactivate", cut.Markup);
    }

    [Fact]
    public void Confirming_delete_calls_the_delete_endpoint_and_raises_OnDeleted()
    {
        // IsDelete=true → the Deactivate button opens the confirm; confirming
        // issues DELETE /contacts/{id} and, on success, raises OnDeleted with
        // the same row the form was bound to.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var detail = SampleDetail();
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            inv => inv.Identifier == "simfAccount.deleteJson")
            .SetResult(ApiResult<bool>.Ok(true));

        AdminContactDetail? deleted = null;
        var cut = RenderComponent<ContactsViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, true)
            .Add(p => p.Initial, detail)
            .Add(p => p.OnDeleted, (AdminContactDetail d) => deleted = d));

        // Open the confirm (the danger button), then confirm it.
        cut.FindAll("button").First(b =>
            b.TextContent.Contains("Admin.Contacts.Action.Deactivate")).Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Contacts.Delete.Title", cut.Markup));
        // The confirm dialog's primary button confirms the deactivation.
        cut.FindAll("button")
            .Last(b => b.TextContent.Contains("Admin.Contacts.Action.Deactivate"))
            .Click();

        cut.WaitForAssertion(() => Assert.Single(deleteHandler.Invocations));
        var call = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/contacts/{detail.Id}", (string)call.Arguments[0]!);
        cut.WaitForAssertion(() => Assert.Equal(detail.Id, deleted?.Id));
    }

    [Fact]
    public void Delete_failure_closes_the_confirm_and_shows_the_error()
    {
        // A server-side block (e.g. 409 CONTACT_IN_USE) must close the confirm
        // first so the error lands on the visible form body, not behind the
        // still-open confirm overlay.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var detail = SampleDetail();
        var failure = ApiResult<bool>.Fail(new ApiError
        {
            Code = "CONTACT_IN_USE",
            Message = "Contact is still linked.",
            MessageArabic = "جهة الاتصال ما زالت مرتبطة.",
        });
        JSInterop.Setup<ApiResult<bool>>(
            inv => inv.Identifier == "simfAccount.deleteJson")
            .SetResult(failure);

        var cut = RenderComponent<ContactsViewDelete>(parameters => parameters
            .Add(p => p.IsDelete, true)
            .Add(p => p.Initial, detail));

        cut.FindAll("button").First(b =>
            b.TextContent.Contains("Admin.Contacts.Action.Deactivate")).Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Contacts.Delete.Title", cut.Markup));
        cut.FindAll("button")
            .Last(b => b.TextContent.Contains("Admin.Contacts.Action.Deactivate"))
            .Click();

        // The error is shown and the confirm dialog is gone.
        cut.WaitForAssertion(() =>
            Assert.Contains("Contact is still linked.", cut.Markup));
        Assert.DoesNotContain("Admin.Contacts.Delete.Title", cut.Markup);
    }
}
