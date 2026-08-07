// Tests: the D-646 SessionModerationDesk entry point. The Sessions grid now
// carries a "Moderate" row action (gated by Questions.Moderate) that navigates to
// the per-session live-Q&A moderation desk (/sessions/{id}/moderate). These bUnit
// tests prove the action is shown only to a moderator, is hidden without the
// permission, and navigates to the desk.
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionsListModerationTests : CpComponentTestBase
{
    // The PassThroughStringLocalizer echoes the resx key, so the button's title
    // attribute is the key itself.
    private const string ModerateTitle = "Admin.Sessions.Action.Moderate";

    private static readonly AdminSessionSummary Row = new(
        Guid.NewGuid(), "S-1", "Opening Session", "الجلسة الافتتاحية",
        Guid.NewGuid(), "Main Hall", "القاعة الرئيسية",
        SimfClock.Now, SimfClock.Now.AddHours(1), 100, true,
        SimfClock.Now);

    // Registers the page's CpPreferences dependency (reads a JS preference in Loose
    // mode → default), optionally grants the Questions.Moderate policy the
    // AuthorizedAction gate checks, and stubs the grid's list call to return one row.
    private void Arrange(bool grantModerate)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        if (grantModerate)
        {
            Authorization.SetPolicies(
                PermissionCatalog.PolicyFor(PermissionCatalog.Questions.Moderate));
        }
        var page = GridPage<AdminSessionSummary>.Of(new[] { Row }, 1, new GridQuery());
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminSessionSummary>>.Ok(page));
    }

    [Fact]
    public void Moderate_action_is_shown_to_a_moderator()
    {
        Arrange(grantModerate: true);

        var cut = RenderComponent<SessionsList>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Row.Title, cut.Markup);
            Assert.Contains(ModerateTitle, cut.Markup);
        });
    }

    [Fact]
    public void Moderate_action_is_hidden_without_the_permission()
    {
        Arrange(grantModerate: false);

        var cut = RenderComponent<SessionsList>();

        cut.WaitForAssertion(() =>
        {
            // The row loaded, but the gated action is absent — an admin without
            // Questions.Moderate never sees the moderation entry point.
            Assert.Contains(Row.Title, cut.Markup);
            Assert.DoesNotContain(ModerateTitle, cut.Markup);
        });
    }

    [Fact]
    public void Moderate_action_navigates_to_the_desk()
    {
        Arrange(grantModerate: true);
        var nav = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<SessionsList>();
        cut.WaitForAssertion(() => Assert.Contains(ModerateTitle, cut.Markup));

        cut.FindAll("button")
            .First(button => button.GetAttribute("title") == ModerateTitle)
            .Click();

        Assert.EndsWith($"/sessions/{Row.Id}/moderate", nav.Uri);
    }
}
