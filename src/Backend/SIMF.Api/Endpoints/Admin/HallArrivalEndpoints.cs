// Tests: SIMF.Api.Tests/HallArrivalScanTests.cs
// Tests: SIMF.Api.Tests/OperationalRateLimitExemptionTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>An operator at a hall door scans an
/// attendee's badge QR to record their arrival (<c>Method = QrScan</c>). Gated
/// by <c>HallArrivals.Record</c> + RequireApprovedAccount; the actor (operator)
/// is the <c>sub</c> claim. The scanned attendee is resolved server-side from
/// the QR id — never trusted from the body beyond the QR string.</summary>
public sealed class RecordQrArrivalEndpoint(IHallAttendanceService service)
    : Endpoint<RecordQrArrivalRequest, ApiResult<QrArrivalResult>>
{
    public override void Configure()
    {
        Post("/admin/sessions/{sessionId:guid}/arrivals");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallArrivals.Record),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // One call per person through a hall door, so it belongs to the
        // operational rate-limit surface. See RateLimitOptions.OperationalPolicy.
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Admin");
    }

    public override async Task HandleAsync(RecordQrArrivalRequest req, CancellationToken ct)
    {
        var operatorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        var result = await service.RecordQrArrivalAsync(operatorId, sessionId, req.QrId, ct);
        await Send.OkAsync(ApiResult<QrArrivalResult>.Ok(result), ct);
    }
}

/// <summary>2026-07-18: an operator at a hall door scans an attendee's badge QR to
/// record their DEPARTURE (check-out), symmetric to <see cref="RecordQrArrivalEndpoint"/>.
/// Gated by <c>HallArrivals.Record</c> + RequireApprovedAccount; the attendee is
/// resolved server-side from the QR id. Reuses the arrival request/result shapes
/// (a QR id in, the resolved attendee + resulting attendance state out).</summary>
public sealed class RecordQrDepartureEndpoint(IHallAttendanceService service)
    : Endpoint<RecordQrArrivalRequest, ApiResult<QrArrivalResult>>
{
    public override void Configure()
    {
        Post("/admin/sessions/{sessionId:guid}/departures");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallArrivals.Record),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        // The exit side of the same door, emptying a hall in one burst.
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Admin");
    }

    public override async Task HandleAsync(RecordQrArrivalRequest req, CancellationToken ct)
    {
        var operatorId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        var result = await service.RecordQrDepartureAsync(operatorId, sessionId, req.QrId, ct);
        await Send.OkAsync(ApiResult<QrArrivalResult>.Ok(result), ct);
    }
}

/// <summary>The sessions the hall-arrival console offers in its picker, gated on
/// the console's OWN permission.
///
/// <para>The console used to call the canonical <c>/admin/sessions/list</c>,
/// which is gated <c>Sessions.View</c>. SecurityTeam - the role seeded with
/// <c>HallArrivals.View</c>, and the role that runs a hall door - does not hold
/// that permission, so the page opened and its first fetch 403'd, leaving an
/// empty picker and nothing the operator could do. Neither widening the
/// canonical list (shared by many pages) nor granting SecurityTeam the whole
/// Sessions module was the right blast radius, so the console gets a read scoped
/// to what it needs.</para>
///
/// <para>Read-only, so <c>HallArrivals.View</c> rather than
/// <c>.Record</c>: an operator who may see the console may fill its picker.
/// Recording an arrival still demands <c>.Record</c> on the endpoints
/// above.</para></summary>
public sealed class ListArrivalSessionsEndpoint(IHallAttendanceService service)
    : Endpoint<GridQuery, ApiResult<GridPage<HallArrivalSessionOption>>>
{
    public override void Configure()
    {
        Post("/admin/hall-arrivals/sessions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.HallArrivals.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<HallArrivalSessionOption>>.Ok(
            await service.ListArrivalSessionsAsync(req, ct)), ct);
}
