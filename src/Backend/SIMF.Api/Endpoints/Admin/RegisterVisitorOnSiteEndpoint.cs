// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-127 — <c>POST /api/v1/admin/visitors/register-onsite</c>. Walk-in
/// registration desk endpoint. Single transaction: create user + profile
/// + interests + mint QR. The account lands directly in
/// <see cref="AccountState.Approved"/> — staff already verified the
/// person face-to-face, no pending queue.
/// </summary>
public sealed class RegisterVisitorOnSiteEndpoint(IAdminUserProvisioningService service)
    : Endpoint<AdminWalkInRegistrationRequest, ApiResult<AdminWalkInRegistrationResponse>>
{
    public override void Configure()
    {
        Post("/admin/visitors/register-onsite");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "On-site walk-in visitor registration. Auto-approves; returns the minted QR id.");
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
