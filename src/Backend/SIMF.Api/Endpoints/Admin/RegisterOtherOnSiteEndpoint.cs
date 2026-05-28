// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-127 — <c>POST /api/v1/admin/others/register-onsite</c>. Twin of
/// <see cref="RegisterVisitorOnSiteEndpoint"/> for the Other kind
/// (exhibitor booth staff, vendors, AV, security, …). Same shape;
/// Interests are ignored on this path.
/// </summary>
public sealed class RegisterOtherOnSiteEndpoint(IAdminUserProvisioningService service)
    : Endpoint<AdminWalkInRegistrationRequest, ApiResult<AdminWalkInRegistrationResponse>>
{
    public override void Configure()
    {
        Post("/admin/others/register-onsite");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "On-site walk-in registration for an Other account. Auto-approves; returns the minted QR id.");
    }

    public override async Task HandleAsync(
        AdminWalkInRegistrationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await service.RegisterOnSiteAsync(
            actorId, UserType.Other, req, ct);
        await Send.OkAsync(ApiResult<AdminWalkInRegistrationResponse>.Ok(response), ct);
    }
}
