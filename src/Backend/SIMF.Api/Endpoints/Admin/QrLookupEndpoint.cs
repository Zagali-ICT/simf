// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (smoke verifies the round-trip)
using FastEndpoints;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-130 — <c>GET /api/v1/admin/qr-lookup/{qrId}</c>. The print-bag
/// station scans (or hand-types) a 12-character Crockford QR id and
/// the endpoint returns the same shape the walk-in success modal
/// renders — name + profile-type + colour + QR id — so the page can
/// reprint the badge. 404 when the QR id is unknown. Admin-only.
/// </summary>
public sealed class QrLookupRequest
{
    public string QrId { get; set; } = string.Empty;
}

public sealed class QrLookupEndpoint(IAdminApprovalReadService service)
    : Endpoint<QrLookupRequest, ApiResult<AdminWalkInRegistrationResponse>>
{
    public override void Configure()
    {
        Get("/admin/qr-lookup/{qrId}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendees.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Resolve a QR id to the visitor / Other badge data for the print-bag station.");
    }

    public override async Task HandleAsync(QrLookupRequest req, CancellationToken ct)
    {
        var match = await service.LookupByQrIdAsync(req.QrId, ct);
        if (match is null)
        {
            throw new ApiException(
                ErrorCodes.NotFound, 404,
                "No badge was found for this QR id.",
                "لم يتم العثور على شارة بهذا الرمز.");
        }
        await Send.OkAsync(ApiResult<AdminWalkInRegistrationResponse>.Ok(match), ct);
    }
}
