// Tests: SIMF.Api.Tests/SessionAttendanceTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Attendance.Abstractions;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Attendance;

/// <summary>The live top-line for the
/// Control Panel session-attendance dashboard: people currently inside a hall,
/// active sessions with attendance, and total arrivals. Read-only aggregate
/// over the existing HallAttendance records; no schema, no writes.
/// Gated by <c>Attendance.View</c> + RequireApprovedAccount.</summary>
public sealed class GetSessionAttendanceSummaryEndpoint(ISessionAttendanceService service)
    : EndpointWithoutRequest<ApiResult<SessionAttendanceSummary>>
{
    public override void Configure()
    {
        Get("/admin/attendance/summary");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<SessionAttendanceSummary>.Ok(
            await service.GetSummaryAsync(ct)), ct);
}

/// <summary>The per-session attendance grid (server-paged): for each
/// active session, the distinct attendee count and the live-now count. Filter
/// on title / code, sort by start time / code / title. Same
/// <c>Attendance.View</c> gate as the summary.</summary>
public sealed class ListSessionAttendanceEndpoint(ISessionAttendanceService service)
    : Endpoint<GridQuery, ApiResult<GridPage<SessionAttendanceRow>>>
{
    public override void Configure()
    {
        Post("/admin/attendance/sessions/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<SessionAttendanceRow>>.Ok(
            await service.ListSessionAttendanceAsync(req, ct)), ct);
}

public sealed class GetSessionPresentAttendeesRoute { public Guid SessionId { get; set; } }

/// <summary>2026-07-18 (live per-session hall view, CP page 2e) — everyone
/// currently present in the session's hall (open attendance rows) with their
/// App-DB profile data + seat. Same <c>Attendance.View</c> gate as the dashboard.</summary>
public sealed class GetSessionPresentAttendeesEndpoint(ISessionAttendanceService service)
    : Endpoint<GetSessionPresentAttendeesRoute, ApiResult<IReadOnlyList<SessionPresentAttendee>>>
{
    public override void Configure()
    {
        Get("/admin/sessions/{sessionId:guid}/present");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetSessionPresentAttendeesRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<SessionPresentAttendee>>.Ok(
            await service.GetPresentAttendeesAsync(req.SessionId, ct)), ct);
}

public sealed class GetAdminSessionSeatMapRoute { public Guid SessionId { get; set; } }

/// <summary>2026-07-18 (live per-session hall view, CP page 2e) — the session's
/// 4-state seat map (available / unavailable / reserved / confirmed) for the CP,
/// reusing the app seat-map read with a null actor (no "my seat" cell). Gated by
/// <c>Attendance.View</c>.</summary>
public sealed class GetAdminSessionSeatMapEndpoint(ISeatReservationService service)
    : Endpoint<GetAdminSessionSeatMapRoute, ApiResult<SessionSeatMap>>
{
    public override void Configure()
    {
        Get("/admin/sessions/{sessionId:guid}/seat-map");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetAdminSessionSeatMapRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<SessionSeatMap>.Ok(
            await service.GetSessionSeatMapAsync(req.SessionId, null, ct)), ct);
}
