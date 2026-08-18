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

/// <summary>The session id from the route plus the grid window from the body.
/// Flattened rather than a nested <c>GridQuery</c> because FastEndpoints binds a
/// route value and body fields onto one request model — the same shape
/// <c>ListSessionSeatReservationsRoute</c> uses for the seat plan.</summary>
public sealed class ListSessionPresentAttendeesRoute
{
    public Guid SessionId { get; set; }
    public int Skip { get; set; }
    public int Top { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public bool SortDescending { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
}

/// <summary>2026-07-18 (live per-session hall view, CP page 2e) — everyone
/// currently present in the session's hall (open attendance rows) with their
/// App-DB profile data + seat, server-paged. Same <c>Attendance.View</c> gate as
/// the dashboard.
/// <para>Paged, and POSTed, for the same reason every other admin list is: an
/// open attendance row stays open until a departure closes it, so an unbounded
/// read of one hall's roster had no ceiling to grow against.</para></summary>
public sealed class ListSessionPresentAttendeesEndpoint(ISessionAttendanceService service)
    : Endpoint<ListSessionPresentAttendeesRoute, ApiResult<GridPage<SessionPresentAttendee>>>
{
    public override void Configure()
    {
        Post("/admin/sessions/{sessionId:guid}/present/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        ListSessionPresentAttendeesRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<SessionPresentAttendee>>.Ok(
            await service.GetPresentAttendeesAsync(
                req.SessionId,
                new GridQuery
                {
                    Skip = req.Skip,
                    Top = req.Top,
                    Search = req.Search,
                    Sort = req.Sort,
                    SortDescending = req.SortDescending,
                    Filters = req.Filters,
                }, ct)), ct);
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
