using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SIMF.Common;

namespace SIMF.Api.Authorization;

/// <summary>
/// Requires the caller to hold one permission code (SIMF-RPM-001 §8). Satisfied
/// by a <c>perm</c> claim equal to <see cref="Code"/> or to the Administrator
/// wildcard <see cref="PermissionCatalog.Wildcard"/>.
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

/// <summary>
/// Materialises a permission policy (<c>perm:&lt;code&gt;</c>) on demand so the
/// ~130 catalogue codes do not each need a registered named policy. Any other
/// policy name (AdministratorOnly, RequireApprovedAccount, the gate / PR
/// policies) falls through to the default provider unchanged.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionCatalog.IsPermissionPolicy(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(PermissionCatalog.CodeFromPolicy(policyName)))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return fallback.GetPolicyAsync(policyName);
    }
}
