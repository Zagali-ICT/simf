// Tests: bUnit coverage for the Website /account/notifications page
// (Notifications.razor + Notifications.razor.cs). D-629 — the page's C# moved
// to a code-behind partial (Website clean-code, Phase 5) and it had zero
// component coverage. The list loads through the same-origin BFF JS bridge
// (simfAccount.postJson), stubbed here on the bUnit JSInterop; unmatched bridge
// calls fall back to the loose default (an empty list).
using System.Globalization;
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Notifications;
using SIMF.Web.Components.Pages.Account;

namespace SIMF.Web.Tests;

public sealed class NotificationsPageTests : WebComponentTestBase
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public NotificationsPageTests()
    {
        // TitleFor/BodyFor pick by CultureInfo.CurrentUICulture directly, so pin
        // it for a deterministic assertion on the English title (restored in
        // Dispose so the thread-static culture cannot leak to another test).
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        // The page + SimfDataGrid reach the DOM through JS; unmatched bridge
        // calls return default (an empty list) unless a test stubs them.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        base.Dispose(disposing);
    }

    [Fact]
    public void Renders_the_page_shell_and_the_mark_all_read_button()
    {
        var cut = RenderComponent<Notifications>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Account.Notifications.Title", cut.Markup);
            Assert.Contains("Account.Notifications.MarkAllRead", cut.Markup);
        });
    }

    [Fact]
    public void Renders_a_notification_row_when_the_bridge_returns_one()
    {
        var page = new GridPage<NotificationDto>
        {
            Items = new[] { Notification("Sea Trials Briefing") },
            Total = 1,
            Top = 25,
        };
        JSInterop.Setup<ApiResult<GridPage<NotificationDto>>>(
            "simfAccount.postJson",
            invocation => invocation.Arguments.Count >= 1
                && (invocation.Arguments[0] as string) == "/account/api/notifications/list")
            .SetResult(ApiResult<GridPage<NotificationDto>>.Ok(page));

        var cut = RenderComponent<Notifications>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Sea Trials Briefing", cut.Markup);
            // An unread row (ReadAt == null) carries the "New" pill.
            Assert.Contains("Account.Notifications.New", cut.Markup);
        });
    }

    private static NotificationDto Notification(string title) =>
        new(Guid.NewGuid(), "AccountApproved", title, "إحاطة التجارب البحرية",
            "The sea trials briefing has been scheduled.", "تمت جدولة إحاطة التجارب البحرية.",
            "Info", ReadAt: null, IsRead: false,
            new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero),
            RelatedEntityType: null, RelatedEntityId: null);
}
