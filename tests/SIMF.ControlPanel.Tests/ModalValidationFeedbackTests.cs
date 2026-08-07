// BUG-004 regression — an empty submit in a SimfModal-hosted Add dialog used to
// look like a dead button: the dialog stayed open, nothing was created, and the
// only error surface was the page-level `_toast` <SimfAlert>, which renders
// UNDER the modal backdrop (.simf-modal is position:fixed; inset:0; z-index:100)
// and is therefore invisible while the dialog is open.
//
// The fix mirrors the canonical CRUD forms (e.g. SessionCategoriesAddEdit):
// a dedicated `_error` rendered as <SimfAlert Variant="error"> INSIDE the
// dialog body, set by a bilingual required-field guard before any API call.
//
// These tests pin both halves: the guard fires, and the message lands inside
// .simf-modal__body (not behind it).
using Bunit;
using SIMF.Common;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ModalValidationFeedbackTests : CpComponentTestBase
{
    public ModalValidationFeedbackTests()
    {
        // D-828 — the FAQ and rating Save buttons are now wrapped in
        // <AuthorizedAction>, so they render only for a holder of the matching
        // Create/Edit permission. The default test identity holds none, which
        // hid the very button these tests click. Granting the permissions a real
        // operator of these pages holds keeps the tests about the validation
        // guard rather than about the auth context.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Create),
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Edit),
            PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Create),
            PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Edit),
            PermissionCatalog.PolicyFor(PermissionCatalog.SessionModerators.Assign));
    }

    /// <summary>The grid toolbar's Add button. The pass-through localizer means
    /// the title attribute is the resx key itself.</summary>
    private const string AddButtonSelector = "button.simf-tbbtn[title='Grid.Add']";

    /// <summary>The dialog's confirming action — Cancel is always the
    /// "secondary" variant, so the primary button in the footer is Save/Submit.</summary>
    private const string DialogSubmitSelector =
        ".simf-modal__footer button.simf-button--primary";

    /// <summary>The error alert must live inside the dialog body, not on the
    /// page behind the backdrop. This selector is the whole point of BUG-004.</summary>
    private const string DialogErrorSelector = ".simf-modal__body .simf-alert--error";

    [Fact]
    public void Faq_group_add_dialog_reports_the_required_fields_on_an_empty_submit()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<FaqManager>();

        cut.Find(AddButtonSelector).Click();
        cut.Find(DialogSubmitSelector).Click();

        Assert.Equal("Admin.Faq.Group.Required", cut.Find(DialogErrorSelector).TextContent.Trim());
    }

    [Fact]
    public void Rating_type_add_dialog_reports_the_required_fields_on_an_empty_submit()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<RatingConfig>();

        cut.Find(AddButtonSelector).Click();
        cut.Find(DialogSubmitSelector).Click();

        Assert.Equal("Admin.RatingConfig.Type.Required",
            cut.Find(DialogErrorSelector).TextContent.Trim());
    }

    [Fact]
    public void Session_moderator_add_dialog_reports_the_required_ids_on_an_empty_submit()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<SessionModeratorsList>();

        cut.Find(AddButtonSelector).Click();
        cut.Find(DialogSubmitSelector).Click();

        // The expected key was "Admin.SessionModerators.Required" until
        // 2026-07-28. DEF-MOD-005 replaced the dialog's two free-text GUID boxes
        // with real session / moderator pickers, so the message it raises on an
        // empty submit is no longer "both ids are required and must be valid
        // ids" but "pick both" — the id-validity half stopped being reachable
        // once the operator can only choose from a list. The page has said
        // PickBoth since commit ca333ff4; only this assertion lagged.
        Assert.Equal("Admin.SessionModerators.PickBoth",
            cut.Find(DialogErrorSelector).TextContent.Trim());
    }

    [Fact]
    public void Empty_submit_never_reaches_the_create_endpoint()
    {
        // The guard must short-circuit BEFORE the POST — the old behaviour let
        // the request go out and then swallowed the 400 behind the backdrop.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<FaqManager>();

        cut.Find(AddButtonSelector).Click();
        cut.Find(DialogSubmitSelector).Click();

        Assert.DoesNotContain(
            JSInterop.Invocations["simfAccount.postJson"],
            invocation => invocation.Arguments.Count > 0
                && (invocation.Arguments[0] as string) == "/account/api/admin/faq/groups");
    }

    [Fact]
    public void Reopening_the_dialog_clears_the_previous_error()
    {
        // Open -> fail -> Cancel -> Open again must not re-show the stale error.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<FaqManager>();

        cut.Find(AddButtonSelector).Click();
        cut.Find(DialogSubmitSelector).Click();
        Assert.Single(cut.FindAll(DialogErrorSelector));

        cut.Find(".simf-modal__footer button.simf-button--secondary").Click();
        cut.Find(AddButtonSelector).Click();

        Assert.Empty(cut.FindAll(DialogErrorSelector));
    }
}
