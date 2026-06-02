// Tests: SIMF.Api.Tests/HallAttendanceTests.cs
// Tests: SIMF.Api.Tests/HallArrivalScanTests.cs (P5.1d — D-244 operator QR scan)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// P5.1 — D-241 (FDS-003 §5.4): the hall-arrival service. Two means: the
/// attendee's device crossing the GPS geofence (D-240/D-241) and an operator
/// scanning the badge QR at the hall door (P5.1d — D-244). Both merge into the
/// one open attendance row. Only the derived enter/leave times are persisted —
/// never the raw coordinates (FDS-003 §10, sensitive PII; continuous
/// movement/dwell is the deferred FR-1103 feature).
/// </summary>
internal sealed class HallAttendanceService(
    SimfAppDbContext appDbContext,
    IQrResolver qrResolver,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<HallAttendanceService> logger) : IHallAttendanceService
{
    public async Task<HallAttendanceStatus> RecordGeofenceArrivalAsync(
        Guid userId, Guid sessionId, double lat, double lon,
        CancellationToken cancellationToken = default)
    {
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            throw new ApiException(ErrorCodes.HallGeofenceInvalid, 400,
                "The reported position is not a valid coordinate.",
                "الموقع المُرسَل ليس إحداثية صحيحة.");
        }

        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new
            {
                s.Id,
                s.HallId,
                s.Hall!.GeofenceCenterLat,
                s.Hall!.GeofenceCenterLon,
                s.Hall!.GeofenceRadiusMeters,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        if (session.GeofenceCenterLat is not { } centerLat
            || session.GeofenceCenterLon is not { } centerLon
            || session.GeofenceRadiusMeters is not { } radius)
        {
            throw new ApiException(ErrorCodes.HallGeofenceNotConfigured, 400,
                "This hall has no geofence; arrival is recorded by a door scan instead.",
                "لا يوجد سياج جغرافي لهذه القاعة؛ يُسجَّل الوصول عبر المسح عند الباب بدلاً من ذلك.");
        }

        var distance = HaversineMeters(lat, lon, centerLat, centerLon);
        if (distance > radius)
        {
            throw new ApiException(ErrorCodes.NotAtVenue, 403,
                "You are not inside the hall yet.",
                "لم تدخل القاعة بعد.");
        }

        var (row, created) = await OpenOrCreateArrivalAsync(
            userId, sessionId, session.HallId, AttendanceMethod.Geofence, cancellationToken);
        if (created)
        {
            await AuditArrivalAsync(userId, sessionId, session.HallId, AttendanceMethod.Geofence, cancellationToken);
            logger.LogInformation(
                "Hall arrival (geofence) recorded for {UserId} at session {SessionId}.",
                userId, sessionId);
        }
        return ToStatus(row);
    }

    public async Task<QrArrivalResult> RecordQrArrivalAsync(
        Guid operatorUserId, Guid sessionId, string qrId,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (qrId ?? string.Empty).Trim();
        var resolved = trimmed.Length == 0
            ? null
            : await qrResolver.ResolveAsync(trimmed, cancellationToken);
        if (resolved is null)
        {
            throw new ApiException(ErrorCodes.AttendeeQrUnknown, 400,
                "That badge QR was not recognised.",
                "لم يتم التعرّف على رمز الشارة.");
        }
        if (resolved.AccountState != AccountState.Approved || resolved.IsLockedOut)
        {
            throw new ApiException(ErrorCodes.AttendeeNotApproved, 403,
                "This attendee's account is not approved for entry.",
                "حساب هذا الحاضر غير معتمد للدخول.");
        }

        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new { s.Id, s.HallId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        // No geofence check — the operator is physically at the door. Merges
        // with any existing open row (e.g. a prior geofence arrival).
        var (row, created) = await OpenOrCreateArrivalAsync(
            resolved.UserId, sessionId, session.HallId, AttendanceMethod.QrScan, cancellationToken);
        if (created)
        {
            await AuditArrivalAsync(resolved.UserId, sessionId, session.HallId, AttendanceMethod.QrScan, cancellationToken, operatorUserId);
            logger.LogInformation(
                "Hall arrival (QR door scan) recorded for {UserId} at session {SessionId} by operator {OperatorId}.",
                resolved.UserId, sessionId, operatorUserId);
        }
        return new QrArrivalResult(
            resolved.UserId, resolved.DisplayName, resolved.DisplayNameArabic, ToStatus(row));
    }

    /// <summary>Returns the attendee's open attendance row for the session,
    /// opening one with <paramref name="method"/> when none exists. Idempotent
    /// under a concurrent race: on the one-open-row unique-index violation it
    /// detaches the losing row and returns the committed one (never a 500) —
    /// mirrors <c>SeatReservationService.PersistWithUniquenessGuardAsync</c>.</summary>
    private async Task<(HallAttendance Row, bool Created)> OpenOrCreateArrivalAsync(
        Guid userId, Guid sessionId, Guid hallId, AttendanceMethod method,
        CancellationToken cancellationToken)
    {
        var open = await OpenRowAsync(userId, sessionId, cancellationToken);
        if (open is not null)
        {
            return (open, false);
        }

        var now = timeProvider.GetUtcNow();
        var row = new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = hallId,
            UserId = userId,
            Method = method,
            EnterUtc = now,
            CreatedAt = now,
        };
        appDbContext.HallAttendances.Add(row);
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            appDbContext.Entry(row).State = EntityState.Detached;
            var existing = await OpenRowAsync(userId, sessionId, cancellationToken);
            return (existing ?? row, false);
        }
        return (row, true);
    }

    private Task AuditArrivalAsync(
        Guid attendeeUserId, Guid sessionId, Guid hallId, AttendanceMethod method,
        CancellationToken cancellationToken, Guid? operatorUserId = null) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallArrivalRecorded,
            Outcome = AuditOutcome.Success,
            ActorUserId = operatorUserId ?? attendeeUserId,
            Detail = operatorUserId is { } op
                ? $"sessionId={sessionId}; hallId={hallId}; method={method}; attendee={attendeeUserId}; operator={op}"
                : $"sessionId={sessionId}; hallId={hallId}; method={method}",
        }, cancellationToken);

    public async Task<HallAttendanceStatus> RecordDepartureAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var open = await OpenRowAsync(userId, sessionId, cancellationToken);
        if (open is null)
        {
            return new HallAttendanceStatus(false, null, null, null);
        }

        var now = timeProvider.GetUtcNow();
        open.LeaveUtc = now;
        open.UpdatedAt = now;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallDepartureRecorded,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"sessionId={sessionId}; hallId={open.HallId}",
        }, cancellationToken);

        return ToStatus(open);
    }

    public async Task<HallAttendanceStatus> GetStatusAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Prefer the open row; otherwise the most recent closed one.
        var row = await appDbContext.HallAttendances
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.UserId == userId)
            .OrderBy(a => a.LeaveUtc == null ? 0 : 1)
            .ThenByDescending(a => a.EnterUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return ToStatus(row);
    }

    private Task<HallAttendance?> OpenRowAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        appDbContext.HallAttendances
            .SingleOrDefaultAsync(
                a => a.SessionId == sessionId && a.UserId == userId && a.LeaveUtc == null,
                cancellationToken);

    private static HallAttendanceStatus ToStatus(HallAttendance? row) =>
        row is null
            ? new HallAttendanceStatus(false, null, null, null)
            : new HallAttendanceStatus(row.LeaveUtc is null, row.EnterUtc, row.LeaveUtc, row.Method);

    /// <summary>Great-circle distance in metres between two WGS-84 points.</summary>
    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
