// Tests: SIMF.Api.Tests/MovementTrackingTests.cs
using System.Globalization;
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>
/// FR-1103 (owner decision Q6) — the attendee-facing capture path for movement /
/// dwell / route tracking. The device uploads a batch of its own position samples;
/// the server resolves each one against the configured hall geofences.
///
/// <para><b>Self-only.</b> The attendee id comes from the <c>sub</c> claim and
/// never from the body, so a caller can post nobody's position but their own —
/// raw GPS is sensitive personal data (FDS-003 §10) and the write side must not
/// accept a subject.</para>
/// </summary>
public sealed class RecordDevicePositionsEndpoint(IMovementTrackingService service)
    : Endpoint<RecordDevicePositionsRequest, ApiResult<RecordDevicePositionsResponse>>
{
    public override void Configure()
    {
        Post("/app/movement/pings");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Sessions");
    }

    public override async Task HandleAsync(
        RecordDevicePositionsRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<RecordDevicePositionsResponse>.Ok(
            await service.RecordPositionsAsync(userId, req, ct)), ct);
    }
}

/// <summary>FR-1103 — the window every movement report is asked for. Both bounds
/// are required: an unbounded aggregate over raw GPS is neither cheap nor
/// defensible.
///
/// <para>Deliberately NOT sealed: <see cref="AttendeeRouteRequest"/> extends it
/// with the subject id, so the two reports cannot drift on how a window is
/// expressed.</para></summary>
public class MovementWindowRequest
{
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>FR-1103 — dwell per hall over a window, aggregated from the raw pings.
/// Admin reporting over sensitive personal data, so it is gated on
/// <c>Attendance.View</c> exactly like the hall-attendance dashboard it sits
/// beside. Returns an empty list while no hall has a geofence boundary.</summary>
public sealed class HallDwellReportEndpoint(IMovementTrackingService service)
    : Endpoint<MovementWindowRequest, ApiResult<IReadOnlyList<HallDwellSummary>>>
{
    public override void Configure()
    {
        Get("/admin/movement/dwell");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(MovementWindowRequest req, CancellationToken ct)
    {
        var (from, to) = MovementWindow.Parse(req.From, req.To);
        await Send.OkAsync(ApiResult<IReadOnlyList<HallDwellSummary>>.Ok(
            await service.DwellByHallAsync(from, to, ct)), ct);
    }
}

/// <summary>FR-1103 — one attendee's route over a window. Same
/// <c>Attendance.View</c> gate as the dwell report; the subject is explicit in the
/// route so the read is auditable as a per-person disclosure.</summary>
public sealed class AttendeeRouteRequest : MovementWindowRequest
{
    public Guid UserId { get; set; }
}

public sealed class AttendeeRouteEndpoint(IMovementTrackingService service)
    : Endpoint<AttendeeRouteRequest, ApiResult<AttendeeRoute>>
{
    public override void Configure()
    {
        Get("/admin/movement/route/{userId:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Attendance.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(AttendeeRouteRequest req, CancellationToken ct)
    {
        var (from, to) = MovementWindow.Parse(req.From, req.To);
        await Send.OkAsync(ApiResult<AttendeeRoute>.Ok(
            await service.RouteForAttendeeAsync(req.UserId, from, to, ct)), ct);
    }
}

/// <summary>FR-1103 — parses and range-checks the <c>?from=</c> / <c>?to=</c>
/// window both movement reports take. Feature-local on purpose: two endpoints
/// share it and nothing else should.</summary>
internal static class MovementWindow
{
    /// <summary>The widest window a single report may span. Bounds the raw-ping
    /// scan and keeps a personal-data read narrow.</summary>
    internal static readonly TimeSpan MaxSpan = TimeSpan.FromDays(7);

    internal static (DateTime From, DateTime To) Parse(string? from, string? to)
    {
        if (!DateTime.TryParse(
                from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom)
            || !DateTime.TryParse(
                to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "Both 'from' and 'to' are required and must be valid timestamps.",
                "الحقلان 'from' و'to' مطلوبان ويجب أن يكونا تاريخين صالحين.");
        }
        if (parsedTo <= parsedFrom)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "'to' must be after 'from'.",
                "يجب أن يكون 'to' بعد 'from'.");
        }
        if (parsedTo - parsedFrom > MaxSpan)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"The reporting window may span at most {MaxSpan.TotalDays:0} days.",
                $"لا يمكن أن تتجاوز فترة التقرير {MaxSpan.TotalDays:0} أيام.");
        }
        return (parsedFrom, parsedTo);
    }
}
