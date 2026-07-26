// Tests: SIMF.Api.Tests/HallAttendanceTests.cs
// Tests: SIMF.Api.Tests/HallArrivalScanTests.cs (P5.1d — D-244 operator QR scan)
// Tests: SIMF.Api.Tests/GateHallDoorChainTests.cs (Both-mode gate → hall-attendance chain)
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Notifications;
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
    INotificationDispatcher notifications,
    TimeProvider timeProvider,
    ILogger<HallAttendanceService> logger) : IHallAttendanceService
{
    // X-3 — how far outside a session's [Start, End] window an arrival is
    // still accepted (early arrivals + a brief post-end tail). Mirrors the CP
    // Hall-Arrivals console's live-session picker filter.
    private static readonly TimeSpan ArrivalGrace = TimeSpan.FromMinutes(15);

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
                s.Start,
                s.End,
                s.HallId,
                s.Hall!.GeofenceCenterLat,
                s.Hall!.GeofenceCenterLon,
                s.Hall!.GeofenceRadiusMeters,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        // X-3 — arrival is only valid while the session is live (its time window,
        // ± a short grace); a stale or future sessionId must not open an
        // attendance row (single-source attendance, FDS-003 §5.4).
        EnsureSessionLiveNow(session.Start, session.End);

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
        // X-4 — apply the SAME core holder-admission checks the perimeter gate
        // engine runs (not-approved, locked, profile-type-inactive) so a hall door
        // can never admit someone a gate would reject. Approved already excludes
        // the Disabled state; the gate's allow-list / gate-active / duplicate-window
        // steps are gate-config-specific and have no hall-door equivalent.
        if (resolved.AccountState != AccountState.Approved
            || resolved.IsLockedOut
            || (resolved.ProfileTypeId is not null && !resolved.ProfileTypeActive))
        {
            throw new ApiException(ErrorCodes.AttendeeNotApproved, 403,
                "This attendee's account is not approved for entry.",
                "حساب هذا الحاضر غير معتمد للدخول.");
        }

        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new { s.Id, s.Start, s.End, s.HallId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        // X-3 — bind the operator door scan to the session's live window (± grace).
        EnsureSessionLiveNow(session.Start, session.End);

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

    public async Task<QrArrivalResult> RecordQrDepartureAsync(
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

        // A check-OUT deliberately does NOT re-run the admission checks the
        // arrival path runs — an attendee already inside the hall must always be
        // allowed to leave — but the session must exist so a bad id is reported
        // rather than silently no-op'ing. No live-window bind either: a departure
        // can be recorded at or just after the session's end.
        var sessionExists = await appDbContext.Sessions.AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.IsActive, cancellationToken);
        if (!sessionExists)
        {
            throw new ApiException(ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");
        }

        // Close the attendee's open row (idempotent: returns Arrived=false when
        // they were not checked in / already left). The seat map's confirmed state
        // clears automatically once the row closes (SeatReservationService reads
        // open rows only).
        var status = await RecordDepartureAsync(resolved.UserId, sessionId, cancellationToken);
        logger.LogInformation(
            "Hall departure (QR door scan) recorded for {UserId} at session {SessionId} by operator {OperatorId}.",
            resolved.UserId, sessionId, operatorUserId);
        return new QrArrivalResult(
            resolved.UserId, resolved.DisplayName, resolved.DisplayNameArabic, status);
    }

    public async Task<bool> RecordGateDoorScanAsync(
        Guid attendeeUserId, Guid hallId, ScanDirection direction,
        bool directionInferred, Guid operatorUserId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        // X-3 (FIX B) — bind the arrival to the session live in this hall right
        // now, using the SAME ±ArrivalGrace window as EnsureSessionLiveNow (the
        // geofence / QR-door / CP picker) so an early or late gate check-in still
        // binds instead of recording nothing. The window bounds are shifted onto
        // the constant `now` (not the DateTimeOffset column) so the filter
        // translates to SQL. A single hall runs one session at a time
        // (BookingOverlap prevents overlapping hall bookings), so this candidate
        // set is tiny; the in-memory ordering prefers the currently-running
        // session over a grace-margin neighbour, then the nearest start (a
        // defensive tie-break).
        var latestStart = now + ArrivalGrace;   // s.Start - grace <= now
        var earliestEnd = now - ArrivalGrace;    // now < s.End + grace
        var sessionId = (await appDbContext.Sessions
                .AsNoTracking()
                .Where(s => s.IsActive && s.HallId == hallId
                    && s.Start <= latestStart && earliestEnd < s.End)
                .Select(s => new { s.Id, s.Start, s.End })
                .ToListAsync(cancellationToken))
            .OrderBy(s => now >= s.Start && now < s.End ? 0 : 1)
            .ThenBy(s => (s.Start - now).Duration())
            .Select(s => (Guid?)s.Id)
            .FirstOrDefault();
        if (sessionId is not { } liveSessionId)
        {
            // No session live in this hall (± grace) — a door scan outside any
            // session window records no hall attendance (the GateScan row stands).
            // DEF-CHK-004 — that used to be silent: the operator saw "Allowed" and
            // the attendance was simply lost. Report it so the gate can raise an
            // advisory notice on the scan result (the entry itself stays allowed).
            logger.LogInformation(
                "Hall-door gate scan for {UserId} in hall {HallId} matched no live session — no attendance recorded.",
                attendeeUserId, hallId);
            return false;
        }

        if (directionInferred)
        {
            // FIX C — on a Both-mode gate the recorded direction is only a per-gate
            // alternation guess; derive the real action from the attendee's open-row
            // state so a re-entry after a departure opens a fresh row instead of a
            // mis-inferred merge. An open row → this scan is the departure.
            // #24 — but only a row opened by a DOOR scan (Method=QrScan) toggles this
            // way. A row opened by the GPS geofence is a cross-channel arrival that
            // the contract says must MERGE into the one open row (interface doc), so
            // the first turnstile pass after a geofence arrival must NOT close it —
            // that used to mark a still-present attendee as departed and under-count
            // live occupancy. Let it fall through to the merge (arrival) path instead.
            var open = await OpenRowAsync(attendeeUserId, liveSessionId, cancellationToken);
            if (open is not null && open.Method != AttendanceMethod.Geofence)
            {
                await RecordDepartureAsync(attendeeUserId, liveSessionId, cancellationToken);
                return true;
            }
            // No open row (or a geofence arrival to merge with) → fall through to the
            // arrival path below.
        }
        else if (direction == ScanDirection.CheckOut)
        {
            // Fixed In/Out gate — its configured direction is authoritative.
            // DEF-CHK-004 — an Out scan for an attendee with NO open row closes
            // nothing, so no attendance was recorded and the operator has to be
            // told (they would otherwise read a plain "Allowed" as "counted"). A
            // non-null Leave is the proof a row was actually closed.
            var departure = await RecordDepartureAsync(
                attendeeUserId, liveSessionId, cancellationToken);
            return departure.Leave is not null;
        }

        // FIX D — capacity is ADVISORY on the passive gate-door path: someone who
        // physically passed a turnstile MUST be counted even past the cap, so this
        // arrival records + warns rather than throwing (operator + geofence
        // arrivals keep the hard HALL_AT_CAPACITY 409).
        var (_, created) = await OpenOrCreateArrivalAsync(
            attendeeUserId, liveSessionId, hallId, AttendanceMethod.QrScan,
            cancellationToken, enforceCapacity: false);
        if (created)
        {
            await AuditArrivalAsync(
                attendeeUserId, liveSessionId, hallId, AttendanceMethod.QrScan,
                cancellationToken, operatorUserId);
            logger.LogInformation(
                "Hall arrival (hall-door gate) recorded for {UserId} at session {SessionId} by operator {OperatorId}.",
                attendeeUserId, liveSessionId, operatorUserId);
        }
        return true;
    }

    /// <summary>Returns the attendee's open attendance row for the session,
    /// opening one with <paramref name="method"/> when none exists. The insert is
    /// idempotent under a concurrent race (see <see cref="InsertArrivalAsync"/>);
    /// when <paramref name="enforceCapacity"/> is set it also refuses to open a row
    /// past the hall's capacity, atomically (see
    /// <see cref="InsertArrivalWithinCapacityAsync"/>).</summary>
    private async Task<(HallAttendance Row, bool Created)> OpenOrCreateArrivalAsync(
        Guid userId, Guid sessionId, Guid hallId, AttendanceMethod method,
        CancellationToken cancellationToken, bool enforceCapacity = true)
    {
        var open = await OpenRowAsync(userId, sessionId, cancellationToken);
        if (open is not null)
        {
            return (open, false);
        }

        // X-2 — a NEW arrival must not push live attendance past the hall's
        // physical capacity. Runs on every arrival means (geofence + door QR +
        // hall-door gate) because it sits in the shared create path; a re-scan that
        // merges into an existing open row (early return above) is never re-checked.
        if (!enforceCapacity)
        {
            // FIX D — passive gate-door path: capacity is advisory. A person who
            // physically passed a turnstile MUST be counted, so record the row and
            // warn rather than drop it (live occupancy would otherwise under-count).
            var advisory = await HallCapacityStateAsync(sessionId, cancellationToken);
            if (advisory.IsOver)
            {
                logger.LogWarning(
                    "Hall {HallId} / session {SessionId} exceeded capacity ({Present}/{Cap}) on a gate-door arrival — recorded (advisory).",
                    hallId, sessionId, advisory.Present, advisory.Cap);
            }
            return await InsertArrivalAsync(userId, sessionId, hallId, method, cancellationToken);
        }

        // Operator QR-door + geofence arrivals — hard cap.
        return await InsertArrivalWithinCapacityAsync(
            userId, sessionId, hallId, method, cancellationToken);
    }

    /// <summary>Inserts a fresh open attendance row. Idempotent under a concurrent
    /// race: on the one-open-row unique-index violation it detaches the losing row
    /// and returns the committed one (never a 500) — mirrors
    /// <c>SeatReservationService.PersistWithUniquenessGuardAsync</c>.</summary>
    private async Task<(HallAttendance Row, bool Created)> InsertArrivalAsync(
        Guid userId, Guid sessionId, Guid hallId, AttendanceMethod method,
        CancellationToken cancellationToken)
    {
        var row = NewArrivalRow(userId, sessionId, hallId, method);
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

    /// <summary>FR-CHK-004 — opens the arrival row only while the session is below
    /// its effective capacity, with the capacity COUNT and the INSERT in ONE
    /// SERIALIZABLE transaction. Count-then-decide outside a transaction let two
    /// concurrent arrivals both read "one place left" and both commit, overfilling
    /// the hall; the count now takes a key-range lock on (SessionId, Leave) so a
    /// concurrent insert cannot slip a phantom row in between the count and the
    /// save. Run through the EF execution strategy so it composes with
    /// <c>EnableRetryOnFailure</c> (a manual transaction under the retrying strategy
    /// throws otherwise) and a serialization/deadlock victim re-runs the whole unit
    /// against the committed rival — no oversell, no over-reject. Mirrors
    /// <c>SeatReservationService.InsertHoldWithinCapacityAsync</c>. Throws
    /// <see cref="ErrorCodes.HallAtCapacity"/> when full.</summary>
    private async Task<(HallAttendance Row, bool Created)> InsertArrivalWithinCapacityAsync(
        Guid userId, Guid sessionId, Guid hallId, AttendanceMethod method,
        CancellationToken cancellationToken)
    {
        HallAttendance? added = null;
        HallAttendance? committed = null;
        var lostTheOpenRowRace = false;
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A retry re-enters here; drop the row a failed attempt left tracked so
            // the next SaveChanges never re-inserts a stale (rolled-back) entity.
            if (added is not null)
            {
                appDbContext.Entry(added).State = EntityState.Detached;
                added = null;
            }
            committed = null;
            lostTheOpenRowRace = false;

            await using var tx = await appDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            var capacity = await HallCapacityStateAsync(sessionId, cancellationToken);
            if (capacity.IsOver)
            {
                return; // full — the transaction rolls back on dispose
            }

            var row = NewArrivalRow(userId, sessionId, hallId, method);
            appDbContext.HallAttendances.Add(row);
            added = row;
            try
            {
                await appDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
            {
                // The one-open-row filtered unique index — a concurrent arrival for
                // the SAME attendee committed first. That is a merge, not a capacity
                // failure, so fall out and return their committed row.
                appDbContext.Entry(row).State = EntityState.Detached;
                added = null;
                lostTheOpenRowRace = true;
                return;
            }
            await tx.CommitAsync(cancellationToken);
            committed = row;
        });

        if (committed is not null)
        {
            return (committed, true);
        }
        if (lostTheOpenRowRace
            && await OpenRowAsync(userId, sessionId, cancellationToken) is { } existing)
        {
            return (existing, false);
        }
        throw new ApiException(ErrorCodes.HallAtCapacity, 409,
            "This hall is at capacity.",
            "بلغت هذه القاعة سعتها القصوى.");
    }

    /// <summary>True only when the store rejected the write on a UNIQUE index —
    /// SQL Server 2601 (duplicate key in a unique index, which is what the
    /// one-open-row FILTERED unique index raises) or 2627 (unique constraint).
    /// Everything else EF wraps in a <see cref="DbUpdateException"/> — above all
    /// the 1205 deadlock victim that two concurrent SERIALIZABLE count-then-insert
    /// units produce by design — MUST propagate so the execution strategy re-runs
    /// the whole unit. Swallowing those turned an ordinary contention retry into a
    /// wrong "this hall is at capacity" 409 at a half-empty hall.</summary>
    private static bool IsUniqueIndexViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private HallAttendance NewArrivalRow(
        Guid userId, Guid sessionId, Guid hallId, AttendanceMethod method)
    {
        var now = timeProvider.GetUtcNow();
        return new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = hallId,
            UserId = userId,
            Method = method,
            Enter = now,
            CreatedAt = now,
        };
    }

    /// <summary>X-2 — the currently-present count against the session's effective
    /// physical capacity (the session <c>CapacityOverride</c>, else the hall
    /// <c>Capacity</c>), plus whether that arrival would exceed it. A capacity of 0
    /// means "no limit configured" and never blocks (<c>IsOver</c> = false). The
    /// open-row count is intentionally per-session (single-source attendance is a
    /// per-session model that relies on the no-overlap booking invariant — a future
    /// overlapping-session feature must revisit this bound). There is no DB
    /// constraint on the attendance row count, so FR-CHK-004 runs this count inside
    /// the enforcing path's SERIALIZABLE transaction — read on its own (the advisory
    /// gate-door path) it is a point-in-time snapshot, not a guarantee. The caller
    /// enforces (hard 409) or warns (advisory gate-door path) per
    /// <c>enforceCapacity</c>.</summary>
    private async Task<(int Present, int Cap, bool IsOver)> HallCapacityStateAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var cap = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => s.CapacityOverride ?? s.Hall!.Capacity)
            .SingleAsync(cancellationToken);
        if (cap <= 0) { return (0, 0, false); }

        var present = await appDbContext.HallAttendances
            .Where(a => a.SessionId == sessionId && a.Leave == null)
            .CountAsync(cancellationToken);
        return (present, cap, present >= cap);
    }

    /// <summary>X-3 — throws when now is outside the session's live window
    /// (± <see cref="ArrivalGrace"/>). Keeps arrival bound to a session that is
    /// actually running, so a stale or future sessionId cannot open a row.</summary>
    private void EnsureSessionLiveNow(DateTimeOffset start, DateTimeOffset end)
    {
        var now = timeProvider.GetUtcNow();
        if (now < start - ArrivalGrace || now > end + ArrivalGrace)
        {
            throw new ApiException(ErrorCodes.SessionNotLive, 409,
                "This session is not open for arrivals right now.",
                "هذه الجلسة ليست مفتوحة لتسجيل الوصول حالياً.");
        }
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
        open.Leave = now;
        open.UpdatedAt = now;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallDepartureRecorded,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = $"sessionId={sessionId}; hallId={open.HallId}",
        }, cancellationToken);

        // D-713 (GAP-A) — leaving the hall closes the attendee's session
        // attendance, so prompt them to rate the session they just left. The
        // dispatcher's DeduplicateByRelatedEntity guard shares one prompt per
        // (session, user) with the clock-end worker, so a re-enter/re-leave or a
        // later clock-end scan never double-prompts.
        await PromptSessionRatingOnDepartureAsync(userId, sessionId, cancellationToken);

        return ToStatus(open);
    }

    /// <summary>D-713 (GAP-A) — fires the "please rate this session" prompt when an
    /// attendee's hall attendance closes on departure. Loads the session title for
    /// the notification body; a dispatch failure is logged and swallowed so it
    /// never fails the departure itself (the leave time is already committed —
    /// mirrors <c>SessionRatingPromptWorker</c>'s per-attendee resilience).</summary>
    private async Task PromptSessionRatingOnDepartureAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            // Respect the CP toggle: no prompt when the "Session" rating type is
            // deactivated in RatingConfig (the rate form is unavailable then) —
            // mirrors SessionRatingPromptWorker so both producers honour the CP.
            var sessionRatingEnabled = await appDbContext.RatingTypes
                .AnyAsync(t => t.Code == "Session" && t.IsActive, cancellationToken);
            if (!sessionRatingEnabled) { return; }

            var session = await appDbContext.Sessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId)
                .Select(s => new { s.Title, s.TitleArabic })
                .SingleOrDefaultAsync(cancellationToken);
            if (session is null) { return; }

            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.SessionRatingRequest,
                Title = "Rate this session",
                TitleArabic = "قيّم هذه الجلسة",
                Body = $"How was \"{session.Title}\"? Tap to rate it.",
                BodyArabic = $"كيف كانت جلسة \"{session.TitleArabic}\"؟ اضغط للتقييم.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = "Session",
                RelatedEntityId = sessionId,
                SendEmail = false,
                // Share the one-per-(session, user) guard with the clock-end worker.
                DeduplicateByRelatedEntity = true,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Session-rating prompt on departure failed for user {UserId} on session {SessionId}.",
                userId, sessionId);
        }
    }

    public async Task<HallAttendanceStatus> GetStatusAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Prefer the open row; otherwise the most recent closed one.
        var row = await appDbContext.HallAttendances
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.UserId == userId)
            .OrderBy(a => a.Leave == null ? 0 : 1)
            .ThenByDescending(a => a.Enter)
            .FirstOrDefaultAsync(cancellationToken);
        return ToStatus(row);
    }

    private Task<HallAttendance?> OpenRowAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        appDbContext.HallAttendances
            .SingleOrDefaultAsync(
                a => a.SessionId == sessionId && a.UserId == userId && a.Leave == null,
                cancellationToken);

    private static HallAttendanceStatus ToStatus(HallAttendance? row) =>
        row is null
            ? new HallAttendanceStatus(false, null, null, null)
            : new HallAttendanceStatus(row.Leave is null, row.Enter, row.Leave, row.Method);

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
