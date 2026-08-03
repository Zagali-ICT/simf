using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SIMF.Common;

namespace SIMF.ControlPanel.Authorization;

/// <summary>
/// Requires the caller to hold one permission code (SIMF-RPM-001 §8). Satisfied
/// by a <c>perm</c> claim — copied from the JWT into the auth cookie at sign-in
/// — equal to <see cref="Code"/> or to the Administrator wildcard
/// <see cref="PermissionCatalog.Wildcard"/>.
/// <para>This is the Control Panel mirror of the API's identical requirement;
/// the API (FastEndpoints) and the CP (Blazor) are separate processes that
/// cannot share an authorization assembly without a new shared project, so the
/// ~50-line policy spine is intentionally duplicated per app.</para>
/// </summary>
public sealed class PermissionRequirement(string code) : IAuthorizationRequirement
{
    public string Code { get; } = code;
}

/// <summary>Passes a <see cref="PermissionRequirement"/> when the principal's
/// <c>perm</c> claims contain the required code or the wildcard.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        foreach (var claim in context.User.FindAll(PermissionCatalog.ClaimType))
        {
            if (claim.Value == PermissionCatalog.Wildcard || claim.Value == requirement.Code)
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Materialises a permission policy (<c>perm:&lt;code&gt;</c>) on
/// demand; any other policy name falls through to the default provider.</summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback;

    /// <summary>D-832 — one built policy per code, for the lifetime of the process.
    ///
    /// <para>A permission policy is immutable and there are at most as many of them
    /// as there are catalogue codes, but this provider is asked for one on EVERY
    /// authorization check and rebuilt it each time: a builder, two backing lists,
    /// two requirement objects, a substring and the policy itself, about ten
    /// allocations per check. Since D-830 the Control Panel asks far more often —
    /// every gated grid button is an AuthorizeView, and the row-end Edit and Delete
    /// gates are instantiated once per row, so a 100-row page re-checks 200 times
    /// on every sort, filter, page change and even a checkbox tick.</para>
    ///
    /// <para>The provider is registered as a singleton, so the cache is safe and
    /// bounded. It changes no decision: the same policy object is handed back
    /// instead of an equal new one.</para></summary>
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> permissionPolicies = new(StringComparer.Ordinal);

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionCatalog.IsPermissionPolicy(policyName))
        {
            var policy = permissionPolicies.GetOrAdd(policyName, static name =>
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(PermissionCatalog.CodeFromPolicy(name)))
                    .Build());
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return fallback.GetPolicyAsync(policyName);
    }
}

/// <summary>
/// Gates a Control Panel page (or component) on a single permission code, e.g.
/// <c>@attribute [RequirePermission(PermissionCatalog.Sessions.View)]</c>. A
/// thin <see cref="AuthorizeAttribute"/> whose policy is the permission policy
/// for the code — usable in a Razor <c>@attribute</c> because the code is a
/// compile-time constant.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode) =>
        Policy = PermissionCatalog.PolicyFor(permissionCode);
}
