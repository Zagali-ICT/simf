// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-127 (amended D-425) — <c>POST /api/v1/admin/visitors/register-onsite</c>.
/// Walk-in registration desk endpoint. Single transaction: create user +
/// profile + interests. The account lands in
/// <see cref="AccountState.PendingApproval"/> with no password and no QR — an
/// admin approves it from the pending-visitors queue, which mints the QR badge
/// (D-386). (D-127 originally auto-approved + minted the QR at the desk; D-425
/// reversed that so every visitor passes through the approval review.)
/// </summary>
public sealed class RegisterVisitorOnSiteEndpoint(IAdminUserProvisioningService service)
    : Endpoint<AdminWalkInRegistrationRequest, ApiResult<AdminWalkInRegistrationResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/register-onsite");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "On-site walk-in visitor registration. Creates a PENDING account (D-425); the QR is minted on approval.");
    }

    public override async Task HandleAsync(
        AdminWalkInRegistrationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        // D-186 review-pass: enforce expectedIsVisitor=true so the
        // Visitors desk cannot accept a partner-side ProfileType.
        var response = await service.RegisterOnSiteAsync(
            actorId, UserType.Visitor, req, ct,
            expectedIsVisitor: true);
        await Send.OkAsync(ApiResult<AdminWalkInRegistrationResponse>.Ok(response), ct);
    }
}
