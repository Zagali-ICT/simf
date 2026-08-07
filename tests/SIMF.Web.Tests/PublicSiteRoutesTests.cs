// Tests: D-774 — the owner removed the login + account area from the public
// Website (2026-07-27). The public site is information-only: visitor accounts
// live in the Flutter app and admin accounts in the Control Panel, so a login
// surface here is unwanted scope and an extra authentication attack surface.
//
// These are ratchet tests. Every assertion below FAILS on the pre-D-774 tree
// (the routes and the cookie/ticket plumbing types were all present) and holds
// afterwards, so re-introducing a Website login page or its BFF plumbing breaks
// the build instead of shipping silently.
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace SIMF.Web.Tests;

public sealed class PublicSiteRoutesTests
{
    private static readonly Assembly WebAssembly = typeof(Strings).Assembly;

    /// <summary>The routes the owner removed on 2026-07-27 (D-774).</summary>
    public static TheoryData<string> RemovedRoutes() =>
    [
        "/login",
        "/login/verify",
        "/forgot-password",
        "/reset-password",
        "/account",
        "/account/profile",
        "/account/notifications",
        "/account/pending",
        "/account/rejected",
    ];

    /// <summary>The Website-local cookie / JWT / one-time-ticket plumbing that
    /// existed only to serve the removed pages.</summary>
    public static TheoryData<string> RemovedPlumbingTypes() =>
    [
        "SIMF.Web.SignInTicketStore",
        "SIMF.Web.SimfCookieRefreshHandler",
        "SIMF.Web.AuthLog",
        "SIMF.Web.Endpoints.AuthEndpoints",
        "SIMF.Web.Endpoints.AccountEndpoints",
        "SIMF.Web.Components.Layout.SessionTimeoutGuard",
    ];

    [Theory]
    [MemberData(nameof(RemovedRoutes))]
    public void Removed_auth_and_account_routes_are_not_routable(string route)
    {
        Assert.DoesNotContain(route, AllRoutes(), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(RemovedPlumbingTypes))]
    public void Removed_auth_plumbing_types_are_gone(string typeName)
    {
        Assert.Null(WebAssembly.GetType(typeName, throwOnError: false));
    }

    // The emailed speaker / delegate confirmation link is anonymous and
    // token-addressed. It is NOT part of the account area and must survive the
    // removal — deleting it would strand every emailed confirmation.
    [Fact]
    public void Anonymous_meeting_confirm_route_is_kept()
    {
        Assert.Contains("/meeting/confirm", AllRoutes(), StringComparer.OrdinalIgnoreCase);
    }

    // No page left on the public site may link to a removed route — the QA
    // element sweep asserts zero dead links, and a stale href is exactly that.
    [Fact]
    public void No_component_declares_a_route_under_the_removed_account_area()
    {
        var accountRoutes = AllRoutes()
            .Where(r => r.StartsWith("/account", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(accountRoutes);
    }

    private static IReadOnlyList<string> AllRoutes() =>
        [.. WebAssembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(a => a.Template)];
}
