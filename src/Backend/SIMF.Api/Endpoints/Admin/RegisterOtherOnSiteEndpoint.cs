// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-127 (amended D-425) — <c>POST /api/v1/admin/others/register-onsite</c>.
/// Twin of <see cref="RegisterVisitorOnSiteEndpoint"/> for the Other kind
/// (exhibitor booth staff, vendors, AV, security, …). Same shape; Interests
/// are ignored on this path. D-425: like the visitor desk, the account now
/// lands <see cref="AccountState.PendingApproval"/> (no QR until approved from
/// the pending-others queue), not auto-approved.
/// </summary>
public sealed class RegisterOtherOnSiteEndpoint(IAdminUserProvisioningService service)
    : Endpoint<AdminWalkInRegistrationRequest, ApiResult<AdminWalkInRegistrationResponse>>
{
    public override void Configure()
    {
        Post("/admin/others/register-onsite");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.RegisterOnsite), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Summary(summary => summary.Summary =
            "On-site walk-in registration for an Other account. Creates a PENDING account (D-425); the QR is minted on approval.");
    }

    public override async Task HandleAsync(
        AdminWalkInRegistrationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        // D-186 + review-pass: walk-in registrations create
        // Visitor-typed accounts; the partner desk enforces
        // expectedIsVisitor=false so a desk that picks an audience
        // ProfileType is rejected with AdminProfileTypeInvalid (the
        // resulting account would otherwise route to the audience
        // queue with the wrong audit-event mapping).
        var response = await service.RegisterOnSiteAsync(
            actorId, UserType.Visitor, req, ct,
            expectedIsVisitor: false);
        await Send.OkAsync(ApiResult<AdminWalkInRegistrationResponse>.Ok(response), ct);
    }
}
