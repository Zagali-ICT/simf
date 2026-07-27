// §6.16 (NAV-001) — a signed-in admin who lacks a permission must land on the
// localized /not-permitted page, NOT on the sign-in form.
//
// AuthorizeRouteView renders its <NotAuthorized> fragment for BOTH the
// unauthenticated case and the authenticated-but-forbidden case. Routes.razor had
// no branch, so every permission denial across the 86 gated CP pages force-reloaded
// the admin onto /login even though their cookie was valid — indistinguishable from
// a session timeout. NotPermitted.razor was built for exactly this and was reachable
// only through Program.cs's cookie AccessDeniedPath, which the Blazor interactive
// router never goes through.
//
// Two tests: the component actually navigates (behaviour), and Routes.razor still
// branches on the authentication state (the wiring that selects it).
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ControlPanel.Components;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class PermissionDeniedRoutingTests : CpComponentTestBase
{
    [Fact]
    public void A_forbidden_signed_in_admin_is_sent_to_the_not_permitted_page()
    {
        var nav = Services.GetRequiredService<NavigationManager>();

        RenderComponent<RedirectToNotPermitted>();

        Assert.Equal("http://localhost/not-permitted", nav.Uri);
    }

    [Fact]
    public void The_redirect_is_a_client_side_navigation_not_a_force_reload()
    {
        // The circuit and the cookie are both healthy on a permission denial, so a
        // full page reload is unnecessary — and a forceLoad here is what made the
        // original defect look like a logout.
        var nav = (FakeNavigationManager)Services.GetRequiredService<NavigationManager>();

        RenderComponent<RedirectToNotPermitted>();

        var navigation = Assert.Single(nav.History);
        Assert.False(navigation.Options.ForceLoad);
    }

    [Fact]
    public void Routes_branches_on_the_authentication_state_before_redirecting()
    {
        var routes = SourceText("src/ControlPanel/SIMF.ControlPanel/Components/Routes.razor");

        // The branch must exist: authenticated -> not-permitted, otherwise -> login.
        Assert.Contains("IsAuthenticated", routes, StringComparison.Ordinal);
        Assert.Contains("<RedirectToNotPermitted />", routes, StringComparison.Ordinal);
        Assert.Contains("<RedirectToLogin />", routes, StringComparison.Ordinal);
    }

    [Fact]
    public void The_not_permitted_page_is_reachable_and_localized()
    {
        var page = SourceText("src/ControlPanel/SIMF.ControlPanel/Components/Pages/NotPermitted.razor");

        Assert.Contains("@page \"/not-permitted\"", page, StringComparison.Ordinal);
        // It must not require a permission of its own, or the redirect would loop.
        Assert.DoesNotContain("RequirePermission", page, StringComparison.Ordinal);
    }

    private static string SourceText(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var absolute = Path.Combine(dir!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolute), $"Expected source file not found: {absolute}");
        return File.ReadAllText(absolute);
    }
}
