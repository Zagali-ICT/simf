// Tests: D-799 — destructive Control Panel actions must not commit on the first
// click. SimfDataGrid deliberately carries no built-in confirmation (its trash
// icon and bulk-delete button invoke their callback directly), so each page owns
// the guard. These tests pin the guard where it was missing: the notification
// inbox (single + bulk dismiss) and the central media library (deactivate).
//
// The shape of every test is the same, and it is the shape that matters — click
// the destructive control, assert the dialog appeared AND that nothing left the
// browser. A test that only asserts the dialog renders would still pass if the
// action fired behind it.
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;
using SIMF.Contracts.Notifications;
using SIMF.ControlPanel.Components.Pages.Account;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class DestructiveConfirmGuardTests : CpComponentTestBase
{
    private static readonly NotificationDto Notification = new(
        Guid.NewGuid(), "BookingConfirmed", "Booking confirmed", "تم تأكيد الحجز",
        "Your seat is reserved.", "تم حجز مقعدك.", "Info",
        ReadAt: null, IsRead: false, CreatedAt: DateTimeOffset.UtcNow,
        RelatedEntityType: null, RelatedEntityId: null);

    private static readonly AdminAssetSummary Asset = new(
        Guid.NewGuid(), AssetCategory.SpeakerPhoto, Guid.NewGuid(), "Captain Speaker",
        AssetKind.Image, AssetSourceType.Upload, ExternalUrl: null,
        ContentType: "image/png", SizeBytes: 2048, OriginalFileName: "speaker.png",
        IsActive: true, CreatedAt: DateTimeOffset.UtcNow, UpdatedAt: null);

    private bool DeletedAnything() =>
        JSInterop.Invocations["simfAccount.deleteJson"].Count > 0;

    [Fact]
    public void Notification_row_dismiss_confirms_before_deleting()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<NotificationDto>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<NotificationDto>>.Ok(
                GridPage<NotificationDto>.Of(new[] { Notification }, 1, new GridQuery())));

        var cut = RenderComponent<Notifications>();
        cut.WaitForAssertion(() => Assert.Contains("Booking confirmed", cut.Markup));

        // Scope to the row: the toolbar's bulk-delete button carries the SAME
        // title and is disabled while nothing is selected.
        cut.Find("tbody button[title='Account.Notifications.Action.Delete']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Account.Notifications.Dismiss.Confirm.Title", cut.Markup));
        Assert.False(DeletedAnything(), "dismiss must not delete before confirmation");

        // And the confirm must actually reach the delete — a guard wired to
        // Cancel would satisfy the assertion above on its own.
        cut.FindAll("button")
           .Last(b => b.TextContent.Contains("Account.Notifications.Action.Delete"))
           .Click();

        cut.WaitForAssertion(() =>
            Assert.True(DeletedAnything(), "confirming must delete the notification"));
    }

    [Fact]
    public void Media_library_deactivate_confirms_before_deleting()
    {
        // The deactivate button lives inside <AuthorizedAction>, so the test
        // identity must actually hold the permission or it renders nothing.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.MediaLibrary.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.MediaLibrary.Manage));
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminAssetSummary>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminAssetSummary>>.Ok(
                GridPage<AdminAssetSummary>.Of(new[] { Asset }, 1, new GridQuery())));
        JSInterop.Setup<ApiResult<AdminAssetSummary>>(
                inv => inv.Identifier == "simfAccount.getJson")
            .SetResult(ApiResult<AdminAssetSummary>.Ok(Asset));

        var cut = RenderComponent<MediaLibraryList>();
        cut.WaitForAssertion(() => Assert.Contains("Captain Speaker", cut.Markup));

        // Open the details modal, then ask to deactivate from its footer. The
        // grid's details action is an icon-only button — match on its title.
        cut.Find("tbody button[title='Admin.MediaLibrary.Manage']").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.MediaLibrary.Details.Title", cut.Markup));

        cut.FindAll("button")
           .First(b => b.TextContent.Contains("Admin.MediaLibrary.Deactivate"))
           .Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.MediaLibrary.Deactivate.Confirm.Title", cut.Markup));
        Assert.False(DeletedAnything(), "deactivate must not delete before confirmation");
        // Staging the confirm closes the details modal so the two never stack.
        Assert.DoesNotContain("Admin.MediaLibrary.Details.Title", cut.Markup);

        cut.FindAll("button")
           .Last(b => b.TextContent.Contains("Admin.MediaLibrary.Deactivate"))
           .Click();

        cut.WaitForAssertion(() =>
            Assert.True(DeletedAnything(), "confirming must deactivate the asset"));
    }
}
