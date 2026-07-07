// Tests: bUnit smoke coverage for the Website /account/profile page
// (UserProfile.razor + UserProfile.razor.cs). D-633 — the page's C# moved to a
// code-behind partial (Website clean-code, Phase 5) and it had zero component
// coverage. The rich happy path (four stubbed BFF loads -> form render) is a
// follow-up; this pins that the page composes and shows its load-error state
// when the BFF profile fetch fails.
using System.Security.Claims;
using Bunit;
using SIMF.Web.Components.Pages.Account;

namespace SIMF.Web.Tests;

public sealed class UserProfilePageTests : WebComponentTestBase
{
    public UserProfilePageTests()
    {
        // The page loads through the BFF JS bridge in OnAfterRenderAsync; with no
        // stub the loose bridge returns null, driving the load-error branch.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_the_load_error_when_the_profile_fetch_fails()
    {
        var cut = RenderComponent<UserProfile>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Account.Profile.LoadFailed", cut.Markup));
    }

    [Fact]
    public void Redirects_a_pending_visitor_off_the_profile_page()
    {
        // The OnParametersSetAsync guard bounces a non-Approved account to the
        // matching state-banner page before any profile data is shown.
        Authorization.SetClaims(new Claim("account_state", "PendingApproval"));

        RenderComponent<UserProfile>();

        Assert.EndsWith("/account/pending", Navigation.Uri);
    }
}
