// Behaviour-pin for the WalkInRegistrationForm submit payload after the
// 2026-07-22 redesign. The rewrite swapped the native gender <select> for a
// SimfSelect and moved the fields into SimfFormSection cards; this test drives a
// full Saudi-branch registration and asserts the exact AdminWalkInRegistrationRequest
// posted to /admin/visitors/register-onsite — the field round-trips (esp. the
// gender SimfSelect and the forced SA nationality) most likely to regress in a
// "must be equivalent" rewrite.
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Organisations;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class WalkInRegistrationFormSubmitTests : CpComponentTestBase
{
    private static readonly Guid TypeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OrgId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static IElement ByLabel(IRenderedComponent<WalkInRegistrationForm> cut, string labelKey)
    {
        var label = cut.FindAll("label.simf-field__label").First(l => l.TextContent.Trim() == labelKey);
        return cut.Find("#" + label.GetAttribute("for"));
    }

    [Fact]
    public void Saudi_walk_in_posts_the_full_payload_including_the_select_round_trips()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Only the loads a Saudi walk-in needs: one audience profile type + one
        // preseeded organisation (the country picker is hidden on the Saudi branch,
        // and regions/interests fall back to empty in Loose mode).
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson", inv => ((string)inv.Arguments[0]!).Contains("profile-types"))
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary>
                {
                    new(TypeId, "VIP", "كبار الشخصيات", "#244A77", "Visitor", "None",
                        IsActive: true, IsVisitor: true, IsAppRegisterable: true),
                }));
        JSInterop.Setup<ApiResult<IReadOnlyList<OrganisationPickerItem>>>(
                "simfAccount.getJson", inv => ((string)inv.Arguments[0]!).Contains("organisations"))
            .SetResult(ApiResult<IReadOnlyList<OrganisationPickerItem>>.Ok(
                new List<OrganisationPickerItem> { new(OrgId, "شركة الاختبار", "Test Co", "Riyadh") }));
        var handler = JSInterop.Setup<ApiResult<AdminWalkInRegistrationResponse>>(
                "simfAccount.postJson", inv => ((string)inv.Arguments[0]!).Contains("register-onsite"))
            .SetResult(ApiResult<AdminWalkInRegistrationResponse>.Ok(
                new AdminWalkInRegistrationResponse(
                    Guid.NewGuid(), "badge@simf.local", "Faisal", "QR123", "VIP", "كبار الشخصيات", "#244A77")));

        var cut = RenderComponent<WalkInRegistrationForm>();

        // Pick the profile-type tile (defaults to the Saudi branch).
        cut.WaitForElement("button.simf-walkin__card").Click();

        // Required identity + contact fields. A plain SimfTextField commits on
        // change; a Numeric one (National ID) strips non-digits live on input.
        ByLabel(cut, "Admin.WalkIn.Field.DisplayName").Change("Faisal Al-Otaibi");
        ByLabel(cut, "Admin.WalkIn.Field.EnglishName").Change("Faisal Al-Otaibi");
        ByLabel(cut, "Admin.WalkIn.Field.ArabicName").Change("فيصل العتيبي");
        ByLabel(cut, "Admin.WalkIn.Field.NationalId").Input("1099887766");
        ByLabel(cut, "Admin.WalkIn.Field.SaudiMobile").Change("+966500112233");

        // Gender SimfSelect round-trip (enum-name option value → Gender.Female).
        ByLabel(cut, "Admin.WalkIn.Field.Gender").Change("Female");

        // Organisation typeahead: the initial load preseeded one org; focusing the
        // search opens the list, then pick the option.
        cut.Find("#walkin-org-search").TriggerEvent("onfocusin", new FocusEventArgs());
        cut.WaitForElement("button.simf-walkin__org-option").Click();

        cut.Find("form").Submit();

        var post = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/visitors/register-onsite", (string)post.Arguments[0]!);
        var body = (AdminWalkInRegistrationRequest)post.Arguments[1]!;
        Assert.Equal(TypeId, body.ProfileTypeId);
        Assert.Equal("Faisal Al-Otaibi", body.DisplayName);
        Assert.Equal("Faisal Al-Otaibi", body.EnglishName);
        Assert.Equal("فيصل العتيبي", body.ArabicName);
        Assert.Equal("1099887766", body.NationalId);
        Assert.Equal("SA", body.NationalityCode);         // Saudi branch forces SA
        Assert.Equal("+966500112233", body.SaudiMobile);
        Assert.Equal(OrgId, body.OrganisationId);
        Assert.Equal(Gender.Female, body.Gender);          // the gender SimfSelect round-trip
        Assert.False(body.IsDelegate);
    }
}
