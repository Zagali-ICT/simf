// Tests: SIMF.Api.Tests/SessionAttendanceTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Attendance.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Attendance;

namespace SIMF.Api.Endpoints.Attendance;

/// <summary>FR-506 (SIMF-SRS-001 §3.5; FDS-003 §5.5) — the live top-line for the
/// Control Panel session-attendance dashboard: people currently inside a hall,
/// active sessions with attendance, and total arrivals. Read-only aggregate
/// over the existing HallAttendance records (D-241); no schema, no writes.
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

/// <summary>FR-506 — the per-session attendance grid (server-paged): for each
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
