// Tests: SIMF.Api.Tests/HallAttendanceTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>P5.1 — D-241 (FDS-003 §5.4, FR-305/506): the attendee-facing hall
/// arrival / departure / status endpoints. The attendee's own device reports
/// its GPS position; the server checks it against the session hall's geofence
/// (D-240). Self-service for an approved account — no admin permission; the
/// actor is the signed-in attendee (the <c>sub</c> claim).</summary>
public sealed class RecordArrivalEndpoint(IHallAttendanceService service)
    : Endpoint<RecordArrivalRequest, ApiResult<HallAttendanceStatus>>
{
    public override void Configure()
    {
        Post("/app/sessions/{sessionId:guid}/arrival");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(RecordArrivalRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var sessionId = Route<Guid>("sessionId");
        var status = await service.RecordGeofenceArrivalAsync(userId, sessionId, req.Lat, req.Lon, ct);
        await Send.OkAsync(ApiResult<HallAttendanceStatus>.Ok(status), ct);
    }
}

public sealed class RecordDepartureEndpoint(IHallAttendanceService service)
    : EndpointWithoutRequest<ApiResult<HallAttendanceStatus>>
{
    public override void Configure()
    {
        Post("/app/sessions/{sessionId:guid}/departure");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var sessionId = Route<Guid>("sessionId");
        var status = await service.RecordDepartureAsync(userId, sessionId, ct);
        await Send.OkAsync(ApiResult<HallAttendanceStatus>.Ok(status), ct);
    }
}

public sealed class GetHallAttendanceStatusEndpoint(IHallAttendanceService service)
    : EndpointWithoutRequest<ApiResult<HallAttendanceStatus>>
{
    public override void Configure()
    {
        Get("/app/sessions/{sessionId:guid}/attendance");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var sessionId = Route<Guid>("sessionId");
        var status = await service.GetStatusAsync(userId, sessionId, ct);
        await Send.OkAsync(ApiResult<HallAttendanceStatus>.Ok(status), ct);
    }
}
