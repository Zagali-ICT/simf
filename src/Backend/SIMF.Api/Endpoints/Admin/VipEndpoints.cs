// Tests: SIMF.Api.Tests/AdminInvitationsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-168 (gap doc G5, PDF §2.7.3) — VIP list (UserProfiles
/// whose ProfileType.Name is in {VVIP, VIP, Gold}) + bulk-notify
/// dispatcher. Same auth policy as the invitation desk; Administrator
/// and PublicRelations share the surface.</summary>
public sealed class ListVipsEndpoint(IAdminInvitationService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminVipSummary>>>
{
    public override void Configure()
    {
        Post("/admin/vips/list");
        Policies(nameof(AuthorizationPolicies.PublicRelationsAccess),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminVipSummary>>.Ok(
            await service.ListVipsAsync(req, ct)), ct);
}

public sealed class NotifyVipsEndpoint(IAdminInvitationService service)
    : Endpoint<AdminNotifyVipsRequest, ApiResult<AdminNotifyVipsResult>>
{
    public override void Configure()
    {
        Post("/admin/vips/notify");
        Policies(nameof(AuthorizationPolicies.PublicRelationsAccess),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminNotifyVipsRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminNotifyVipsResult>.Ok(
            await service.NotifyVipsAsync(actorId, req, ct)), ct);
    }
}
