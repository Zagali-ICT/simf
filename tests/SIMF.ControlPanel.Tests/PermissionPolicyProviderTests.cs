using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using SIMF.Common;
using SIMF.ControlPanel.Authorization;
using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// D-832 — the dynamic permission-policy provider.
///
/// <para>It is asked for a policy on every authorization check, and since D-830 the
/// Control Panel asks far more often: each gated grid button is an
/// <c>AuthorizeView</c>, and the row-end Edit and Delete gates are instantiated once
/// per row, so a 100-row page re-checks 200 times on every sort, filter, page change
/// and checkbox tick. The policy for a code is immutable and there are at most as
/// many as there are catalogue codes, so it is built once and kept.</para>
///
/// <para>These pin both halves: that the cache is real (the same instance comes
/// back), and that it changed no decision — the policy still carries the
/// authenticated-user requirement and the right code, and a policy name that is not
/// a permission still falls through to the default provider.</para>
/// </summary>
public sealed class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider Provider() =>
        new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task The_same_code_returns_the_same_policy_instance()
    {
        var provider = Provider();
        var policyName = PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Edit);

        var first = await provider.GetPolicyAsync(policyName);
        var second = await provider.GetPolicyAsync(policyName);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task Different_codes_get_different_policies()
    {
        // The obvious way to break the cache: key it on something that is not the
        // code, and every gate in the Control Panel silently checks the first code
        // ever asked for.
        var provider = Provider();

        var edit = await provider.GetPolicyAsync(
            PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Edit));
        var delete = await provider.GetPolicyAsync(
            PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Delete));

        Assert.NotSame(edit, delete);
        Assert.Equal(
            PermissionCatalog.Sessions.Edit,
            edit!.Requirements.OfType<PermissionRequirement>().Single().Code);
        Assert.Equal(
            PermissionCatalog.Sessions.Delete,
            delete!.Requirements.OfType<PermissionRequirement>().Single().Code);
    }

    [Fact]
    public async Task A_permission_policy_still_requires_an_authenticated_user()
    {
        var provider = Provider();

        var policy = await provider.GetPolicyAsync(
            PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Edit));

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task A_non_permission_policy_name_falls_through_to_the_default_provider()
    {
        // Anything not prefixed perm: belongs to the framework's own provider. Caching
        // must not swallow those, or a named policy registered in Program.cs stops
        // resolving.
        var options = new AuthorizationOptions();
        options.AddPolicy("SomethingElse", builder => builder.RequireAssertion(_ => true));
        var provider = new PermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("SomethingElse");
        var unknown = await provider.GetPolicyAsync("NotRegisteredAtAll");

        Assert.NotNull(policy);
        Assert.Null(unknown);
    }
}
