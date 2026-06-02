// Tests: SIMF.Api.Tests/HallAttendanceTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// P5.1 — D-241 (FDS-003 §5.4): the attendee-facing hall-arrival service. The
/// device reports a GPS point; the server checks it against the session hall's
/// geofence (D-240) and opens / closes the one attendance row. Only the derived
/// enter/leave times are persisted — never the raw coordinates (FDS-003 §10,
/// sensitive PII; continuous movement/dwell is the deferred FR-1103 feature).
/// </summary>
internal sealed class HallAttendanceService(
    SimfAppDbContext appDbContext,
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

        var open = await OpenRowAsync(userId, sessionId, cancellationToken);
        if (open is not null)
        {
            // Already arrived — idempotent.
            return ToStatus(open);
        }

        var now = timeProvider.GetUtcNow();
        var row = new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = session.HallId,
            UserId = userId,
            Method = AttendanceMethod.Geofence,
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
            // A concurrent arrival (e.g. a double-tap) raced the one-open-row
            // unique index (D-241). Arrival is idempotent: detach our losing row
            // and return the row the other request committed — never a 500.
            // Mirrors SeatReservationService.PersistWithUniquenessGuardAsync.
            appDbContext.Entry(row).State = EntityState.Detached;
            var existing = await OpenRowAsync(userId, sessionId, cancellationToken);
            return ToStatus(existing);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallArrivalRecorded,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"sessionId={sessionId}; hallId={session.HallId}; method=Geofence",
        }, cancellationToken);
        logger.LogInformation(
            "Hall arrival (geofence) recorded for {UserId} at session {SessionId}.",
            userId, sessionId);

        return ToStatus(row);
    }

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
