// Tests: SIMF.Api.Tests/OfflineBadgeUploadTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Badges;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-819 — <c>POST /api/v1/admin/offline/batch</c>. The offline badge desk's
/// reconciliation upload: a desk that registered visitors with no network posts
/// its whole shift here once it is back on the venue link.
///
/// <para>Carries the <b>operational</b> rate-limit policy (i.e. none). A desk
/// coming back online after a blackout uploads hundreds of rows in a burst, and
/// throttling that would strand printed badges outside the system exactly when
/// the gates need them. The desk permission is the control.</para>
///
/// <para>Gated on <c>WalkInMode:OfflineUpload</c>: with the switch off this
/// answers 403 <c>OFFLINE_UPLOAD_DISABLED</c> and writes nothing.</para>
/// </summary>
public sealed class OfflineBadgeBatchEndpoint(IOfflineBadgeUploadService service)
    : Endpoint<OfflineBadgeBatchRequest, ApiResult<OfflineBadgeBatchResponse>>
{
    public override void Configure()
    {
        Post("/admin/offline/batch");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting(
            RateLimitOptions.OperationalPolicy));
        Summary(summary => summary.Summary =
            "Uploads a batch of badges registered at an offline desk (D-819).");
    }

    public override async Task HandleAsync(
        OfflineBadgeBatchRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var response = await service.UploadAsync(actorId, req, ct);
        await Send.OkAsync(ApiResult<OfflineBadgeBatchResponse>.Ok(response), ct);
    }
}
