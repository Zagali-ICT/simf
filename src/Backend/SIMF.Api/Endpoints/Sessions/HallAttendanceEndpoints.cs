// Tests: SIMF.Api.Tests/HallAttendanceTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>The attendee-facing hall
/// arrival / departure / status endpoints. The attendee's own device reports
/// its GPS position; the server checks it against the session hall's geofence.
/// Self-service for an approved account — no admin permission; the
/// actor is the signed-in attendee (the <c>sub</c> claim).</summary>
public sealed class RecordArrivalEndpoint(IHallAttendanceService service)
    : Endpoint<RecordArrivalRequest, ApiResult<HallAttendanceStatus>>
{
    public override void Configure()
    {
        Post("/app/sessions/{sessionId:guid}/arrival");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        // Deliberately NOT on the operational (unlimited) policy.
        // The rate limits were lifted off the STAFF-operated surface, where an
        // operator holding a named permission is the control. This is attendee
        // SELF-SERVICE — any approved account can call it, and it does a session
        // read plus a Haversine plus attendance writes. Exempting it removed the
        // global per-IP cap from an endpoint with no permission gate at all.
        Tags("Sessions");
    }

    public override async Task HandleAsync(RecordArrivalRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();
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
        // Deliberately NOT on the operational (unlimited) policy.
        // The rate limits were lifted off the STAFF-operated surface, where an
        // operator holding a named permission is the control. This is attendee
        // SELF-SERVICE — any approved account can call it, and it does a session
        // read plus a Haversine plus attendance writes. Exempting it removed the
        // global per-IP cap from an endpoint with no permission gate at all.
        Tags("Sessions");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();
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
        var userId = User.ActorId();
        var sessionId = Route<Guid>("sessionId");
        var status = await service.GetStatusAsync(userId, sessionId, ct);
        await Send.OkAsync(ApiResult<HallAttendanceStatus>.Ok(status), ct);
    }
}
