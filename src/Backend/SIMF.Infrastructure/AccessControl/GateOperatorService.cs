// Tests: SIMF.Api.Tests/GateScanTests.cs
// Tests: SIMF.Api.Tests/GateHallDoorChainTests.cs (DEF-CHK-004 — the hall-attendance
//        chain and the advisory NoticeMessage an allowed scan can carry)
// Tests: SIMF.Api.Tests/GateScanIdempotencyRecoveryTests.cs (a committed scan whose
//        idempotency back-fill never landed still replays as itself, not as a blank
//        denial)
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Gates;
using SIMF.Domain.AccessControl;
using SIMF.Domain.Editions;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>
/// Operator surface + 13-step constraint engine. Every
/// recorded denial emits exactly one <see cref="DenialReasonCode"/>;
/// HTTP-level errors (404 / 403 / 409 / 429 / 503) ride on
/// <see cref="GateScanResult.Kind"/> for the endpoint to translate.
/// </summary>
internal sealed class GateOperatorService(
    SimfAppDbContext appDbContext,
    IQrResolver qrResolver,
    IGateConfigCache configCache,
    IGateFailureCircuit failureCircuit,
    IHallAttendanceService hallAttendance,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IOptionsMonitor<WalkInModeOptions> walkInMode,
    ILogger<GateOperatorService> logger) : IGateOperatorService
{
    private const string ArabicLanguageCode = "ar";
    // GateScan.QrIdAtScan column width (GateScanConfiguration.HasMaxLength(96)).
    // A normalised QR longer than this would truncate the append-only scan row on
    // insert, so it is denied as QrUnknown rather than stored.
    //
    // The limit grew from 32 to 64 and then to 96 when the badge tag went
    // to the full 16 bytes. The bound is NOT removed: it still guards the insert
    // against an over-length mis-scan. It is wide because a badge is no longer
    // only a 12-character serial — an offline event badge is an encrypted payload
    // of ~61 characters, and the whole blob is stored so the audit row is exactly
    // what was presented at the gate.
    private const int QrIdAtScanMaxLength = 96;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<OperatorGateAssignment>> ListMyAssignmentsAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.GateAssignments.AsNoTracking()
            .Where(a => a.UserId == operatorUserId && a.IsActive)
            .Join(appDbContext.Gates.AsNoTracking(),
                a => a.GateId, g => g.Id,
                (a, g) => new { g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive })
            .OrderBy(g => g.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(g => new OperatorGateAssignment(
            g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive)).ToList();
    }

    public async Task<GateOfflineConfig> GetOfflineConfigAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default)
    {
        var options = walkInMode.CurrentValue;
        var now = timeProvider.SimfNow();
        var armed = options.AcceptOfflineBadgesActive(now);

        var gates = await appDbContext.GateAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == operatorUserId && assignment.IsActive)
            .Join(appDbContext.Gates.AsNoTracking().Include(gate => gate.AllowedProfileTypes),
                assignment => assignment.GateId, gate => gate.Id, (_, gate) => gate)
            .OrderBy(gate => gate.Code)
            .ToListAsync(cancellationToken);

        // The allow-list is stored as profile-type Guids, but an offline device
        // only ever sees the CODE inside the decrypted badge, so translate here
        // rather than shipping a Guid the scanner could not match. A type with
        // no code yet is dropped: admitting on a code of 0 would admit every
        // un-coded type at once.
        var codeByProfileType = await appDbContext.ProfileTypes.AsNoTracking()
            .Where(type => type.Code != 0)
            .ToDictionaryAsync(type => type.Id, type => type.Code, cancellationToken);

        var rules = gates.Select(gate => new GateOfflineRule(
            gate.Id,
            gate.Code,
            gate.AllowedProfileTypes
                .Select(allow => codeByProfileType.TryGetValue(allow.ProfileTypeId, out var code)
                    ? code
                    : (short)0)
                .Where(code => code != 0)
                .Distinct()
                .OrderBy(code => code)
                .ToList(),
            gate.HallId is not null,
            gate.IsActive)).ToList();

        // The key travels ONLY while offline badges are armed, and ONLY to a
        // caller who actually works a gate. Disarming therefore stops handing it
        // to new devices — the lever available if one goes missing, together
        // with rotating the version.
        //
        // The assignment requirement matters as much as the arming
        // one. Gates.Operate is held by every Staff and Moderator app account,
        // not just the provisioned scanner tablets, so without this the key would
        // land in unencrypted preferences on every staff phone at the event and
        // nobody could say how many copies existed.
        var handOutKey = armed && rules.Count > 0;

        return new GateOfflineConfig(
            BadgeKey: handOutKey ? options.BadgeKey : null,
            BadgeKeyVersion: options.BadgeKeyVersion,
            PreviousBadgeKey: handOutKey && !string.IsNullOrWhiteSpace(options.PreviousBadgeKey)
                ? options.PreviousBadgeKey
                : null,
            PreviousBadgeKeyVersion: options.PreviousBadgeKeyVersion,
            SessionWalkIn: options.SessionWalkInActive(now),
            IssuedAt: now,
            Gates: rules);
    }

    /// <summary>How long a downloaded roster may be trusted. Long enough to
    /// survive a session's worth of network loss, short enough that a revocation
    /// issued after the last sync cannot be honoured indefinitely — which is the
    /// one thing a device still cannot decide for itself.</summary>
    private static readonly TimeSpan RosterValidity = TimeSpan.FromHours(12);

    public async Task<GateOfflineRoster> GetOfflineRosterAsync(
        Guid operatorUserId, DateTime? since,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();

        // The operator's OWN halls, reached through their own gate assignments.
        // Same scoping the badge key already gets, and for a stronger reason: a
        // roster is attendee names and movements, and Gates.Operate is held by
        // every Staff and Moderator account rather than only the provisioned
        // tablets.
        var hallIds = await appDbContext.GateAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == operatorUserId && assignment.IsActive)
            .Join(appDbContext.Gates.AsNoTracking(),
                assignment => assignment.GateId, gate => gate.Id, (_, gate) => gate)
            .Where(gate => gate.IsActive && gate.HallId != null)
            .Select(gate => gate.HallId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (hallIds.Count == 0)
        {
            // No hall door to serve, so nothing to expect. An empty roster, not
            // an error: a perimeter-only operator is an ordinary case.
            return new GateOfflineRoster(now, now.Add(RosterValidity), []);
        }

        var reservations = appDbContext.SeatReservations.AsNoTracking()
            // Only a CONFIRMED, still-held reservation counts. A pending or
            // rejected request must never read as an admitted seat at the door,
            // and a released one is somebody else's seat now.
            .Where(reservation => reservation.Status == BookingStatus.Approved
                && reservation.ReleasedAt == null
                && reservation.ReservedForProfileId != null
                && reservation.Session != null
                && hallIds.Contains(reservation.Session.HallId));

        if (since is { } cursor)
        {
            // The delta. CreatedAt is the only monotonic stamp a reservation
            // carries, so a device asks for what has appeared since its last
            // successful sync rather than pulling the hall again.
            reservations = reservations.Where(reservation => reservation.CreatedAt > cursor);
        }

        // Projected flat and mapped in memory: the record constructor with a
        // conditional inside it is not translatable, and forcing it to be would
        // mean contorting the shape the device consumes to suit the query.
        var rows = await reservations
            .Select(reservation => new
            {
                ProfileId = reservation.ReservedForProfileId!.Value,
                reservation.ReservedForProfile!.Name,
                reservation.ReservedForProfile.NameArabic,
                TypeCode = reservation.ReservedForProfile.ProfileType!.Code,
                reservation.ReservedForProfile.AdmissionState,
                reservation.SessionId,
                reservation.Session!.Start,
                reservation.Session.End,
                reservation.Session.HallId,
                reservation.RowLabel,
                reservation.SeatNumber,
            })
            .OrderBy(row => row.Start)
            .ToListAsync(cancellationToken);

        var attendees = rows.Select(row => new GateOfflineRosterEntry(
            row.ProfileId,
            row.Name,
            row.NameArabic,
            row.TypeCode,
            // A decided boolean, not the raw state. The device should not be
            // reimplementing admission rules the server already owns, and a
            // second copy of that logic is a second thing to get wrong.
            row.AdmissionState == AccountState.Approved,
            row.SessionId,
            row.Start,
            row.End,
            row.HallId,
            // Null for general admission, and for a hall admitted by booking
            // rather than by seat — the row still says "this person is expected
            // in this session", which is the question the door asks.
            row.RowLabel,
            row.SeatNumber)).ToList();

        return new GateOfflineRoster(now, now.Add(RosterValidity), attendees);
    }

    public async Task<GateScanResult> RecordScanAsync(
        GateScanContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = await configCache.GetAsync(context.GateId, cancellationToken);
        if (snapshot is null)
        {
            return Routing(GateScanResultKind.GateNotFound, "GATE_NOT_FOUND");
        }
        if (!snapshot.AssignedOperatorUserIds.Contains(context.OperatorUserId))
        {
            return Routing(GateScanResultKind.NotAssigned, "GATE_OPERATOR_NOT_ASSIGNED");
        }
        if (failureCircuit.IsOpen(context.GateId))
        {
            return Routing(GateScanResultKind.CircuitOpen, "GATE_FAILURE_CIRCUIT_OPEN");
        }

        // Idempotency-key precedence (SIMF-API-GATES-001 §9): header wins.
        var idempotencyKey = !string.IsNullOrWhiteSpace(context.HeaderIdempotencyKey)
            ? context.HeaderIdempotencyKey
            : context.Request.IdempotencyKey;
        string? requestHash = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            requestHash = HashRequest(context.GateId, context.Request, idempotencyKey);
            var replay = await TryReplayAsync(
                idempotencyKey, context.GateId, requestHash,
                context.AcceptLanguage, cancellationToken);
            if (replay is not null) { return replay; }
        }

        var qr = QrId.Normalise(context.Request.Qr ?? string.Empty);

        // Bound raised to the widened nvarchar(96) column) — a
        // normalised value longer than the column would truncate the append-only
        // scan row on insert (a 500 for an ordinary over-length mis-scan). Deny it
        // as the documented QrUnknown at HTTP 200, storing a length-capped
        // QrIdAtScan so the log row still fits — the same denial the unresolved-QR
        // path below records.
        if (qr.Length > QrIdAtScanMaxLength)
        {
            return await RecordDenialAsync(
                context, qrIdAtScan: qr[..QrIdAtScanMaxLength], denialCtx: DenialContext.Empty,
                direction: ResolveDirection(snapshot, context.Request.RequestedDirection, null),
                reason: DenialReasonCode.QrUnknown,
                requestHash, idempotencyKey, cancellationToken);
        }

        QrResolution? resolution;
        try
        {
            resolution = await qrResolver.ResolveAsync(qr, cancellationToken);
        }
        catch (Exception ex)
        {
            // G-3 — a QR-resolver / backend fault (NOT a policy denial) is exactly
            // the systemic failure the failure-rate circuit exists to trip on.
            // Feed the circuit, then let the exception surface as a 5xx.
            logger.LogError(ex, "QR resolver faulted on gate {GateId}.", context.GateId);
            await failureCircuit.RecordDenialAsync(context.GateId, cancellationToken);
            throw;
        }
        if (resolution is null)
        {
            return await RecordDenialAsync(
                context, qrIdAtScan: qr, denialCtx: DenialContext.Empty,
                direction: ResolveDirection(snapshot, context.Request.RequestedDirection, null),
                reason: DenialReasonCode.QrUnknown,
                requestHash, idempotencyKey, cancellationToken);
        }

        var denialCtx = DenialContext.From(resolution);
        var coldStart = ResolveDirection(snapshot, context.Request.RequestedDirection, null);

        // The year currently open. A badge carries the edition it was issued for,
        // and last year's must not open this year's gate — which is the only
        // expiry a minted QR has ever had, this resolver having matched on value
        // alone until now.
        var openEditionYear = await appDbContext.EventEdition
            .AsNoTracking()
            .Where(edition => edition.Id == EventEdition.SingletonId)
            .Select(edition => (int?)edition.Year)
            .SingleOrDefaultAsync(cancellationToken);

        // Steps 5–9: per-row predicate → denial reason, ordered. Step 9.5 is the
        // edition check, a reserved hook until now. Step 11.5
        // (booking-required) is implemented and runs after the
        // allow-list below, because it needs the resolved direction.
        var simpleChecks = new (bool failed, DenialReasonCode reason)[]
        {
            (!snapshot.IsActive,                              DenialReasonCode.GateInactiveAtScan),
            (resolution.AccountState != AccountState.Approved
                && resolution.AccountState != AccountState.Disabled,
                                                              DenialReasonCode.HolderNotApproved),
            (resolution.AccountState == AccountState.Disabled, DenialReasonCode.HolderDisabled),
            (resolution.IsLockedOut,                          DenialReasonCode.HolderLocked),
            (resolution.ProfileTypeId is not null && !resolution.ProfileTypeActive,
                                                              DenialReasonCode.ProfileTypeInactive),
            // Step 9.5 — the badge is from a closed edition. Deliberately NOT
            // given a distinct operator message: a scan must never tell the
            // holder which half of the check failed. A zero on the record means
            // the attendee predates the column, and is left alone rather than
            // locked out by a schema change.
            (openEditionYear is { } openYear
                && resolution.EditionYear != 0
                && resolution.EditionYear != openYear,
                                                              DenialReasonCode.OutsideTimeWindow),
        };
        foreach (var (failed, reason) in simpleChecks)
        {
            if (!failed) { continue; }
            return await RecordDenialAsync(context, qrIdAtScan: qr, denialCtx,
                direction: coldStart, reason: reason,
                requestHash, idempotencyKey, cancellationToken);
        }

        // Step 12 — 5-second duplicate-absorption window keyed (GateId,
        // UserProfileId). Run BEFORE the allow-list and direction queries so
        // an absorbed duplicate skips both. The single query returns enough
        // to satisfy both the duplicate path and the direction inference.
        var windowCutoff = timeProvider.SimfNow() - DuplicateWindow;
        var lastAllowed = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == context.GateId
                     && s.UserProfileId == resolution.UserProfileId
                     && s.Outcome == ScanOutcome.Allowed)
            .OrderByDescending(s => s.ScannedAt)
            .Select(s => new { s.Id, s.Direction, s.ScannedAt })
            .FirstOrDefaultAsync(cancellationToken);
        // On a Both-mode gate the operator can deliberately switch
        // دخول/خروج and re-scan the same badge within the window (e.g. a quick
        // correction, or a fast in-then-out). That is an intentional new
        // movement, NOT an accidental duplicate, so it must NOT be absorbed.
        // Every other case (no explicit direction → inference; same direction
        // re-scan; a fixed In/Out gate) is still absorbed as before.
        var requestedDirection = context.Request.RequestedDirection;
        var isDeliberateDirectionSwitch = snapshot.DirectionMode == DirectionMode.Both
            && requestedDirection is { } requested
            && lastAllowed is not null
            && requested != lastAllowed.Direction;
        if (lastAllowed is not null && lastAllowed.ScannedAt >= windowCutoff
            && !isDeliberateDirectionSwitch)
        {
            var replay = new GateScanResponse(
                lastAllowed.Id, ScanOutcome.Allowed,
                lastAllowed.Direction, lastAllowed.ScannedAt,
                denialCtx.ToProfile(), null, null);
            return new GateScanResult(GateScanResultKind.Recorded, replay, false, null);
        }
        var direction = ResolveDirection(
            snapshot, requestedDirection, lastAllowed?.Direction);

        // Step 11 — allow-list. Empty raw list = pass; filtered-empty (L-15)
        // denies all.
        if (snapshot.AllowedProfileTypeIdsRaw.Count > 0
            && (resolution.ProfileTypeId is null
                || !snapshot.AllowedProfileTypeIdsFiltered.Contains(resolution.ProfileTypeId.Value)))
        {
            return await RecordDenialAsync(context, qrIdAtScan: qr, denialCtx,
                direction: direction, reason: DenialReasonCode.ProfileTypeNotAllowed,
                requestHash, idempotencyKey, cancellationToken);
        }

        // Step 11.5 — a SESSION HALL door additionally requires the
        // attendee to be registered for the session running behind it. This is
        // the third of the three access rules (approved at the main gate,
        // profile type allowed at any gate, registered at a session hall) and
        // was previously unimplemented: DenialReasonCode.BookingRequiredMissing
        // existed as a reserved hook with no writer, so any valid badge opened
        // every hall.
        //
        // Applied to ENTRIES only. A departure is never blocked — someone
        // already inside must always be able to leave — and CheckHallEntry
        // returns AlreadyInside for an attendee with an open row, which also
        // covers a Both-mode gate whose direction was only inferred.
        //
        // The walk-in mode relaxes THIS rule and only this one; approved and
        // profile-type-allowed above always hold.
        if (snapshot.HallId is { } sessionHallId && direction == ScanDirection.CheckIn)
        {
            // Asked by PROFILE, which every attendee has. Bookings and attendance
            // are both keyed by it, so an attendee with no account is now answered
            // on their real registration instead of being assumed unregistered.
            var eligibility = await hallAttendance.CheckHallEntryEligibilityAsync(
                resolution.UserProfileId, sessionHallId, cancellationToken);

            if (eligibility == HallEntryEligibility.NotRegistered
                && !walkInMode.CurrentValue.SessionWalkInActive(timeProvider.SimfNow()))
            {
                return await RecordDenialAsync(context, qrIdAtScan: qr, denialCtx,
                    direction: direction,
                    reason: DenialReasonCode.BookingRequiredMissing,
                    requestHash, idempotencyKey, cancellationToken);
            }
        }

        return await RecordAllowedAsync(context, qr, resolution, direction,
            snapshot.HallId, snapshot.DirectionMode == DirectionMode.Both,
            requestHash, idempotencyKey, cancellationToken);
    }

    public async Task<OperatorDailyReport> GetMyDailyReportAsync(
        Guid operatorUserId, Guid? gateId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
        var fromUtc = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
        var toUtc = fromUtc.AddDays(1).AddTicks(-1);

        var query = appDbContext.GateScans.AsNoTracking()
            .Where(s => s.ScannedByUserId == operatorUserId
                     && s.ScannedAt >= fromUtc && s.ScannedAt <= toUtc);
        if (gateId is { } gid) { query = query.Where(s => s.GateId == gid); }

        // The Rows DISPLAY grid stays capped at the 500 most-recent scans (bounds
        // the wire payload); the Totals + DenialBreakdown below are computed over
        // the FULL day server-side, not from this capped list (A8).
        var rows = await query
            .OrderByDescending(s => s.ScannedAt)
            .Take(500)
            // The visitor name comes off the scan row's own immutable snapshot, not
            // from a profile-then-Identity pair of round trips. An attendee with no
            // account is the ordinary case, so resolving the name through
            // SimfUser.DisplayName left every walk-in badge nameless on this report
            // while the value it should have shown sat unread on the row.
            .Select(s => new
            {
                s.Id, s.ScannedAt, s.Outcome, s.Direction,
                s.ScannedDisplayName, s.DenialReasonCode,
            })
            .ToListAsync(cancellationToken);

        // A8 — full-day aggregates over `query` (server-side GROUP BY), NOT the
        // Take(500) grid, so a gate with >500 scans/day reports correct Totals +
        // buckets. Allowed + denied come from ONE GroupBy(Outcome) so both counts
        // read the same snapshot — neither `denied` (nor allowed) can go negative
        // under a concurrent insert the way subtracting two independently-timed
        // counts could. EF can GROUP BY the int enum key but cannot translate
        // enum.ToString(), so the reason code is formatted + ordered in memory.
        var outcomeCounts = await query
            .GroupBy(s => s.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var allowed = outcomeCounts
            .FirstOrDefault(o => o.Outcome == ScanOutcome.Allowed)?.Count ?? 0;
        var denied = outcomeCounts
            .FirstOrDefault(o => o.Outcome == ScanOutcome.Denied)?.Count ?? 0;
        var denialCounts = await query
            .Where(s => s.Outcome == ScanOutcome.Denied && s.DenialReasonCode != null)
            .GroupBy(s => s.DenialReasonCode!.Value)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var denialBuckets = denialCounts
            .Select(b => new OperatorDenialBucket(b.Code.ToString(), b.Count))
            .OrderByDescending(b => b.Count)
            .ToList();

        var rowDtos = rows.Select(r => new OperatorScanRow(
            r.Id, r.ScannedAt, r.Outcome, r.Direction,
            r.ScannedDisplayName,
            r.DenialReasonCode)).ToList();

        return new OperatorDailyReport(
            operatorUserId, fromUtc, toUtc,
            new OperatorDailyReportTotals(allowed, denied),
            denialBuckets, rowDtos);
    }

    public async Task<GateVisitorsListResult> ListGateVisitorsAsync(
        Guid operatorUserId, Guid gateId, GateVisitorsListRequest request,
        CancellationToken cancellationToken = default)
    {
        // Authority — same pattern as RecordScanAsync: gate must exist and
        // the operator must be assigned to it.
        var snapshot = await configCache.GetAsync(gateId, cancellationToken);
        if (snapshot is null)
        {
            return new GateVisitorsListResult(GateVisitorsListResultKind.GateNotFound, null);
        }
        if (!snapshot.AssignedOperatorUserIds.Contains(operatorUserId))
        {
            return new GateVisitorsListResult(GateVisitorsListResultKind.NotAssigned, null);
        }

        var pageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 50, 1, 200);
        var afterId = DecodeCursor(request.Cursor);
        // Default the outcome to Allowed when not specified — that's the
        // "who's currently inside" use case the staff app actually wants.
        var outcome = request.Outcome ?? ScanOutcome.Allowed;

        var query = appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == gateId);
        if (afterId is { } cursorAfter)
        {
            query = query.Where(s => s.Id > cursorAfter);
        }
        query = query.Where(s => s.Outcome == outcome);
        if (request.Direction is { } dir)
        {
            query = query.Where(s => s.Direction == dir);
        }
        if (request.Since is { } since)
        {
            query = query.Where(s => s.ScannedAt >= since);
        }
        if (request.Until is { } until)
        {
            query = query.Where(s => s.ScannedAt < until);
        }

        var items = await query
            .OrderBy(s => s.Id)
            .Take(pageSize)
            .Select(s => new GateVisitorListItem(
                s.Id, s.ScannedAt, s.Direction, s.Outcome,
                s.UserProfileId, s.QrIdAtScan,
                // Snapshot columns on the scan row — no cross-DB JOIN.
                s.ScannedDisplayName, s.ScannedProfileTypeName,
                s.DenialReasonCode))
            .ToListAsync(cancellationToken);

        var nextCursor = items.Count == pageSize
            ? EncodeCursor(items[^1].ScanId)
            : null;

        return new GateVisitorsListResult(
            GateVisitorsListResultKind.Ok,
            new GateVisitorsListResponse(items, nextCursor, timeProvider.SimfNow()));
    }

    // Opaque cursor encoding for the gate-visitors list. Single
    // long-valued cursor (lastSeenScanId); base64 over a tiny JSON blob
    // so the wire format can grow without breaking older clients.
    private static string EncodeCursor(long lastSeenScanId)
    {
        var json = JsonSerializer.Serialize(new { lastId = lastSeenScanId });
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private static long? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) { return null; }
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("lastId", out var prop)
                && prop.TryGetInt64(out var id))
            {
                return id;
            }
        }
        catch
        {
            // A malformed cursor is treated as "no cursor" — the staff app
            // will see the first page again rather than a 400, which is
            // friendlier under the operator-poll loop.
        }
        return null;
    }

    // ---- internals ----

    private GateScanResult Routing(GateScanResultKind kind, string code) =>
        new(kind, EmptyResponse(timeProvider.SimfNow()), false, code);

    private static GateScanResponse EmptyResponse(DateTime scannedAt) =>
        new(0, ScanOutcome.Denied, ScanDirection.CheckIn, scannedAt, null, null, null);

    /// <summary>Resolves the direction a scan records. A fixed In / Out
    /// gate always records its configured direction. A <c>Both</c> gate honours
    /// the operator's explicit <paramref name="requested"/> choice (the staff
    /// console's دخول/خروج toggle); when the operator did not pick one it falls
    /// back to the prior alternation inference (cold start = CheckIn, then
    /// alternate from the holder's last allowed scan).</summary>
    private static ScanDirection ResolveDirection(
        GateConfigSnapshot snapshot, ScanDirection? requested, ScanDirection? lastDirection)
    {
        if (snapshot.DirectionMode == DirectionMode.In) { return ScanDirection.CheckIn; }
        if (snapshot.DirectionMode == DirectionMode.Out) { return ScanDirection.CheckOut; }
        if (requested is { } chosen) { return chosen; }
        return lastDirection switch
        {
            ScanDirection.CheckIn => ScanDirection.CheckOut,
            ScanDirection.CheckOut => ScanDirection.CheckIn,
            _ => ScanDirection.CheckIn,
        };
    }

    private async Task<GateScanResult?> TryReplayAsync(
        string idempotencyKey, Guid gateId, string requestHash,
        string? acceptLanguage, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.SimfNow() - IdempotencyRetention;
        var prior = await appDbContext.ScanIdempotencies.AsNoTracking()
            .Where(r => r.Key == idempotencyKey && r.GateId == gateId && r.StoredAt >= cutoff)
            .SingleOrDefaultAsync(cancellationToken);
        if (prior is null) { return null; }
        if (!string.Equals(prior.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Routing(GateScanResultKind.IdempotencyConflict, "IDEMPOTENCY_KEY_CONFLICT");
        }
        var replay = await LoadReplayAsync(prior, acceptLanguage, cancellationToken);
        return new GateScanResult(GateScanResultKind.Recorded, replay, true, null);
    }

    /// <summary>
    /// Stages the idempotency row for a just-processed scan, to be
    /// committed in the same SaveChanges as the GateScan. Upserts on the composite
    /// PK (Key, GateId): a returning badge that re-scans with the same key AFTER
    /// the 24h replay window has a stale row that <see cref="TryReplayAsync"/>
    /// filters out as absent, but the PK still holds it — so a blind Add would
    /// collide (the returning-badge 500). Refreshing the stale row in place avoids
    /// the collision. ScanId is left null here and back-filled by the caller after
    /// SaveChanges assigns the GateScan identity; a back-fill that never lands is
    /// recovered by <see cref="FindReplayScanAsync"/> rather than replaying blank.
    /// </summary>
    private async Task StageIdempotencyAsync(
        string idempotencyKey, Guid gateId, string requestHash,
        DateTime now, CancellationToken cancellationToken)
    {
        var existing = await appDbContext.ScanIdempotencies
            .SingleOrDefaultAsync(
                r => r.Key == idempotencyKey && r.GateId == gateId, cancellationToken);
        if (existing is not null)
        {
            existing.RequestHash = requestHash;
            existing.ScanId = null;
            existing.StoredAt = now;
            return;
        }

        appDbContext.ScanIdempotencies.Add(new ScanIdempotency
        {
            Key = idempotencyKey,
            GateId = gateId,
            RequestHash = requestHash,
            ScanId = null,
            StoredAt = now,
        });
    }

    /// <summary>Persists a freshly built <see cref="GateScan"/> (with its
    /// staged idempotency row) and back-fills the idempotency row's ScanId. A
    /// concurrent same-key retry (both requests clear <see cref="TryReplayAsync"/>
    /// before either commits) or a key reused past the 24h replay window collides on
    /// the append-only <c>UX_GateScan_Idempotency</c> / <c>PK_ScanIdempotency</c>
    /// uniqueness. The idempotency contract is a replay, not
    /// a 500, so that duplicate-key collision is recovered into the prior committed
    /// scan — mirroring <c>HallAttendanceService.OpenOrCreateArrivalAsync</c> and
    /// <c>SeatReservationService.PersistWithUniquenessGuardAsync</c>. Returns the
    /// replay result to hand back to the caller, or <c>null</c> when the insert
    /// committed normally.</summary>
    private async Task<GateScanResult?> TrySaveScanAsync(
        GateScan scan, string? idempotencyKey, Guid gateId,
        string? acceptLanguage, CancellationToken cancellationToken)
    {
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            !string.IsNullOrWhiteSpace(idempotencyKey)
            && ex.ViolatesAnyIndex("UX_GateScan_Idempotency", "PK_ScanIdempotency"))
        {
            return await RecoverIdempotentReplayAsync(
                scan, idempotencyKey!, gateId, acceptLanguage, cancellationToken);
        }

        // ScanId is now populated by the IDENTITY column. Back-fill the idempotency
        // row so the ordinary replay is a single keyed read.
        //
        // This is a SECOND statement, issued after the scan has already committed, so
        // it is a best-effort pointer and NOT the replay's only source of truth: a
        // cancelled token, a transient failure here, or a process restart would
        // otherwise strand the committed idempotency row on a null ScanId forever.
        // FindReplayScanAsync recovers from the scan row's own idempotency index in
        // that case, which is what keeps a timed-out-then-retried allowed scan from
        // replaying as a blank denial.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await appDbContext.ScanIdempotencies
                .Where(r => r.Key == idempotencyKey && r.GateId == gateId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ScanId, scan.Id),
                    cancellationToken);
        }
        return null;
    }

    /// <summary>Recovers a duplicate-key idempotency collision into a replay:
    /// detaches the losing scan insert and its staged idempotency row so the context
    /// is clean, then loads and returns the prior committed scan (the byte-identical
    /// replay the idempotency contract promises). The <see cref="TrySaveScanAsync"/>
    /// filter guarantees an idempotency row is committed for the key, so the prior
    /// lookup resolves.</summary>
    private async Task<GateScanResult> RecoverIdempotentReplayAsync(
        GateScan losingScan, string idempotencyKey, Guid gateId,
        string? acceptLanguage, CancellationToken cancellationToken)
    {
        appDbContext.Entry(losingScan).State = EntityState.Detached;
        foreach (var staged in appDbContext.ChangeTracker
                     .Entries<ScanIdempotency>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified)
                     .ToList())
        {
            staged.State = EntityState.Detached;
        }

        var prior = await appDbContext.ScanIdempotencies.AsNoTracking()
            .Where(r => r.Key == idempotencyKey && r.GateId == gateId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Gate scan idempotency collision with no prior row to replay.");

        logger.LogInformation(
            "Recovered an idempotent replay on gate {GateId} after a duplicate-key scan insert.",
            gateId);
        var replay = await LoadReplayAsync(prior, acceptLanguage, cancellationToken);
        return new GateScanResult(GateScanResultKind.Recorded, replay, true, null);
    }

    private async Task<GateScanResult> RecordAllowedAsync(
        GateScanContext context, string qr, QrResolution resolution,
        ScanDirection direction, Guid? hallDoorHallId, bool hallDoorDirectionInferred,
        string? requestHash, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.SimfNow();
        var scan = BuildScan(context, qr, ScanOutcome.Allowed,
            denialReason: null, userProfileId: resolution.UserProfileId,
            direction: direction, now: now, idempotencyKey: idempotencyKey,
            scannedDisplayName: resolution.DisplayName,
            scannedProfileTypeName: resolution.ProfileTypeName);
        appDbContext.GateScans.Add(scan);

        var response = new GateScanResponse(
            0, ScanOutcome.Allowed, direction, now,
            new GateScanUserProfile(
                resolution.UserProfileId, resolution.DisplayName,
                resolution.DisplayNameArabic, resolution.ProfileTypeId,
                resolution.ProfileTypeName, resolution.ProfileTypePageColor),
            null, null);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && requestHash is not null)
        {
            await StageIdempotencyAsync(
                idempotencyKey, context.GateId, requestHash, now, cancellationToken);
        }
        var replayResult = await TrySaveScanAsync(
            scan, idempotencyKey, context.GateId, context.AcceptLanguage, cancellationToken);
        if (replayResult is not null) { return replayResult; }

        failureCircuit.RecordAllowed(context.GateId);
        logger.LogInformation(
            "Allowed scan {ScanId} on gate {GateId} for visitor {ProfileId}",
            scan.Id, context.GateId, resolution.UserProfileId);

        // By design, a hall-door gate (HallId set) feeds hall attendance
        // for the session live in that hall. Best-effort: the gate scan is already
        // committed, so a chain failure is logged and swallowed rather than failing
        // the operator's scan (mirrors HallAttendanceService's departure-hook
        // resilience). Perimeter gates (HallId null) are unchanged. The attendee is
        // carried as resolution.UserProfileId, which HallAttendance is keyed by, so a
        // holder with no Identity account is recorded like any other.
        // FIX C — a Both-mode gate's direction is only an alternation guess, so the
        // chain derives the real action from attendance state (directionInferred);
        // a fixed In/Out gate stays authoritative.
        // DEF-CHK-004 — a hall-door scan can admit the holder while recording NO
        // session attendance (no session live in the hall, or an Out scan with no
        // open row to close). That used to be silent, so the operator saw a plain
        // "Allowed" while the attendance was lost. Carry an advisory notice on the
        // (still Allowed) result, resolved to the caller's Accept-Language through
        // the same helper the denial messages use.
        string? notice = null;
        if (hallDoorHallId is { } hallId)
        {
            try
            {
                var attendanceRecorded = await hallAttendance.RecordGateDoorScanAsync(
                    resolution.UserProfileId, hallId, direction,
                    hallDoorDirectionInferred, context.OperatorUserId, cancellationToken);
                if (!attendanceRecorded)
                {
                    notice = NoticeMessageFor(
                        GateScanNotice.AttendanceNotRecorded, context.AcceptLanguage);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Hall-attendance chain failed for scan {ScanId} on gate {GateId} (hall {HallId}).",
                    scan.Id, context.GateId, hallId);
                notice = NoticeMessageFor(
                    GateScanNotice.AttendanceChainFailed, context.AcceptLanguage);
            }
        }

        return new GateScanResult(GateScanResultKind.Recorded,
            response with { ScanId = scan.Id, NoticeMessage = notice }, false, null);
    }

    /// <summary>DEF-CHK-004 — the advisory notes an ALLOWED scan can carry. Kept
    /// private to this service: the wire contract only ships the already-localized
    /// <c>GateScanResponse.NoticeMessage</c>, mirroring how the denial reasons
    /// surface as <c>DenialMessage</c>.</summary>
    private enum GateScanNotice
    {
        /// <summary>The chain ran but recorded nothing — no session live in the
        /// hall, a check-out that found no open attendance row to close, or an
        /// arrival whose insert the store rejected. The service reports all of
        /// them identically (one bool), so the wording must not name a single
        /// cause; the exact reason is in the server log.</summary>
        AttendanceNotRecorded,
        AttendanceChainFailed,
    }

    private static string NoticeMessageFor(GateScanNotice notice, string? acceptLanguage)
    {
        var (en, ar) = NoticeMessages(notice);
        return string.Equals(acceptLanguage, ArabicLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? ar : en;
    }

    private static (string en, string ar) NoticeMessages(GateScanNotice notice) =>
        notice switch
        {
            GateScanNotice.AttendanceNotRecorded =>
                ("Entry allowed, but no session attendance was recorded for this scan.",
                 "تم السماح بالدخول، ولكن لم يتم تسجيل حضور الجلسة لهذا المسح."),
            _ =>
                ("Entry allowed, but the session attendance could not be recorded.",
                 "تم السماح بالدخول، ولكن تعذّر تسجيل حضور الجلسة."),
        };

    private async Task<GateScanResult> RecordDenialAsync(
        GateScanContext context,
        string qrIdAtScan,
        DenialContext denialCtx,
        ScanDirection direction,
        DenialReasonCode reason,
        string? requestHash, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.SimfNow();

        // G-5 — debounce denied scans the same way allowed scans are absorbed (the
        // 5s window in RecordScanAsync only covered Allowed). A malfunctioning or
        // button-mashed scanner repeating the same denial (same QR + reason) within
        // DuplicateWindow gets the prior denial replayed instead of writing a fresh
        // row, re-auditing, or (post G-3) feeding the circuit. Keyed per-scan (QR +
        // reason) so a denied scan's null UserProfileId (QrUnknown) is handled, and
        // runs AFTER the idempotency-key replay (TryReplayAsync) so the two dedupe
        // mechanisms layer: exact-key replay first, then per-scan denial absorption.
        var denialWindowCutoff = now - DuplicateWindow;
        var priorDenial = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.GateId == context.GateId
                     && s.Outcome == ScanOutcome.Denied
                     && s.QrIdAtScan == qrIdAtScan
                     && s.DenialReasonCode == reason
                     && s.ScannedAt >= denialWindowCutoff)
            .OrderByDescending(s => s.ScannedAt)
            .Select(s => new { s.Id, s.Direction, s.ScannedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (priorDenial is not null)
        {
            var absorbed = new GateScanResponse(
                priorDenial.Id, ScanOutcome.Denied, priorDenial.Direction,
                priorDenial.ScannedAt, denialCtx.ToProfile(),
                reason, MessageFor(reason, context.AcceptLanguage));
            return new GateScanResult(GateScanResultKind.Recorded, absorbed, false, null);
        }

        var scan = BuildScan(context, qrIdAtScan, ScanOutcome.Denied,
            denialReason: reason, userProfileId: denialCtx.UserProfileId,
            direction: direction, now: now, idempotencyKey: idempotencyKey,
            scannedDisplayName: denialCtx.DisplayName,
            scannedProfileTypeName: denialCtx.ProfileTypeName);
        appDbContext.GateScans.Add(scan);

        var message = MessageFor(reason, context.AcceptLanguage);
        var response = new GateScanResponse(
            0, ScanOutcome.Denied, direction, now,
            denialCtx.ToProfile(),
            reason, message);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && requestHash is not null)
        {
            await StageIdempotencyAsync(
                idempotencyKey, context.GateId, requestHash, now, cancellationToken);
        }
        var replayResult = await TrySaveScanAsync(
            scan, idempotencyKey, context.GateId, context.AcceptLanguage, cancellationToken);
        if (replayResult is not null) { return replayResult; }

        await auditLog.WriteFailureAsync(
            AuditEvents.GateScanDenied,
            context.OperatorUserId,
            detail: $"gateId={context.GateId}; reason={reason}; scanId={scan.Id}; corr={context.CorrelationId}",
            cancellationToken: cancellationToken);
        // G-3 — only SYSTEM-fault denials count toward the failure-rate circuit.
        // Benign POLICY denials (unknown QR, holder-not-approved, wrong profile
        // type, …) are the operator's normal traffic and must never trip a 5-minute
        // gate outage for everyone. Every reason the engine emits today is a policy
        // denial, so none feed the circuit here; genuine infrastructure faults feed
        // it from the QR-resolver catch block above instead.
        if (IsSystemFaultDenial(reason))
        {
            await failureCircuit.RecordDenialAsync(context.GateId, cancellationToken);
        }

        return new GateScanResult(GateScanResultKind.Recorded,
            response with { ScanId = scan.Id }, false, null);
    }

    private static GateScan BuildScan(
        GateScanContext context, string qrIdAtScan, ScanOutcome outcome,
        DenialReasonCode? denialReason, Guid? userProfileId, ScanDirection direction,
        DateTime now, string? idempotencyKey,
        string? scannedDisplayName, string? scannedProfileTypeName) =>
        new()
        {
            GateId = context.GateId,
            UserProfileId = userProfileId,
            // Snapshot the visitor identity at scan time so the
            // log row survives even if the linked Identity-DB row is
            // later deleted or renamed.
            ScannedDisplayName = scannedDisplayName,
            ScannedProfileTypeName = scannedProfileTypeName,
            QrIdAtScan = qrIdAtScan,
            Direction = direction,
            Outcome = outcome,
            DenialReasonCode = denialReason,
            ScannedAt = now,
            ClientScannedAt = context.Request.ClientScannedAt,
            ScannedByUserId = context.OperatorUserId,
            Source = context.Request.Source,
            CorrelationId = context.CorrelationId,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            IdempotencyKey = idempotencyKey,
        };

    /// <summary>Resolves the scan a prior idempotency row stands for. The fast path
    /// is the back-filled <see cref="ScanIdempotency.ScanId"/>; when that is null the
    /// scan is recovered from the append-only log through the unique filtered
    /// <c>UX_GateScan_Idempotency</c> index on (IdempotencyKey, GateId), which is
    /// written as part of the scan row itself and is therefore committed whenever the
    /// idempotency row is.
    ///
    /// The recovery is not defensive padding. The back-fill is a SECOND statement
    /// issued after the scan has already committed, so a cancelled token (the
    /// scanner's HTTP request timing out and the client disconnecting), a transient
    /// failure of that statement, or a process restart between the two leaves a
    /// committed idempotency row permanently pointing at nothing. A scanner that
    /// times out retries with the same key, and without this the replay would answer
    /// every one of those retries with a blank denial — ScanId 0, outcome Denied, no
    /// reason, no visitor profile — for an attendee whose committed scan says Allowed,
    /// for the whole 24h retention window. The same gap opens on a concurrent same-key
    /// retry that reads the row in between the winner's commit and its back-fill.
    ///
    /// Setting ScanId inside the original SaveChanges is not available as an
    /// alternative: GateScan.Id is IDENTITY-generated and ScanIdempotency has no EF
    /// relationship to propagate it from.</summary>
    private async Task<GateScan?> FindReplayScanAsync(
        ScanIdempotency prior, CancellationToken cancellationToken)
    {
        if (prior.ScanId is { } scanId)
        {
            return await appDbContext.GateScans.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == scanId, cancellationToken);
        }

        return await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.IdempotencyKey == prior.Key && s.GateId == prior.GateId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<GateScanResponse> LoadReplayAsync(
        ScanIdempotency prior, string? acceptLanguage,
        CancellationToken cancellationToken)
    {
        var scan = await FindReplayScanAsync(prior, cancellationToken);
        if (scan is null) { return EmptyResponse(prior.StoredAt); }

        // One App-DB read. The display name is the scan row's own snapshot — the very
        // value the original response carried — so the replay is the byte-identical
        // one the idempotency contract promises. Resolving it through
        // SimfUser.DisplayName instead needed a second database and returned blank for
        // every holder with no account, which is the ordinary badge holder.
        GateScanUserProfile? profile = null;
        if (scan.UserProfileId is { } pid)
        {
            var row = await appDbContext.UserProfiles.AsNoTracking()
                .Where(p => p.Id == pid)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.NameArabic,
                    p.ProfileTypeId,
                    ProfileTypeName = p.ProfileType != null ? p.ProfileType.Name : null,
                    ProfileTypePageColor = p.ProfileType != null ? p.ProfileType.PageColor : null,
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (row is not null)
            {
                profile = new GateScanUserProfile(
                    row.Id, scan.ScannedDisplayName ?? row.Name, row.NameArabic,
                    row.ProfileTypeId,
                    row.ProfileTypeName,
                    row.ProfileTypePageColor);
            }
        }

        string? message = scan.DenialReasonCode is { } reason
            ? MessageFor(reason, acceptLanguage) : null;
        return new GateScanResponse(
            scan.Id, scan.Outcome, scan.Direction, scan.ScannedAt,
            profile, scan.DenialReasonCode, message);
    }

    /// <summary>G-3 — classifies a denial as a SYSTEM fault (a backend/infra
    /// problem that should count toward the failure-rate circuit) vs a benign
    /// POLICY denial (the constraint engine rejecting a scan on its merits). Every
    /// reason the engine emits today is a policy denial; the switch is explicit so
    /// a future infrastructure-fault reason must opt in deliberately.</summary>
    private static bool IsSystemFaultDenial(DenialReasonCode reason) =>
        reason switch
        {
            DenialReasonCode.QrUnknown => false,
            DenialReasonCode.GateInactiveAtScan => false,
            DenialReasonCode.HolderNotApproved => false,
            DenialReasonCode.HolderDisabled => false,
            DenialReasonCode.HolderLocked => false,
            DenialReasonCode.ProfileTypeInactive => false,
            DenialReasonCode.OutsideTimeWindow => false,
            DenialReasonCode.ProfileTypeNotAllowed => false,
            DenialReasonCode.BookingRequiredMissing => false,
            _ => false,
        };

    private static string MessageFor(DenialReasonCode reason, string? acceptLanguage)
    {
        var (en, ar) = DenialMessages(reason);
        return string.Equals(acceptLanguage, ArabicLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? ar : en;
    }

    private static (string en, string ar) DenialMessages(DenialReasonCode reason) =>
        reason switch
        {
            DenialReasonCode.QrUnknown =>
                ("This QR code is not recognised.",
                 "هذا الرمز غير معروف."),
            DenialReasonCode.GateInactiveAtScan =>
                ("This gate is currently inactive.",
                 "هذه البوابة غير نشطة حالياً."),
            DenialReasonCode.HolderNotApproved =>
                ("This visitor's account has not been approved.",
                 "لم يتم اعتماد حساب هذا الزائر."),
            DenialReasonCode.HolderDisabled =>
                ("This visitor's account is disabled.",
                 "حساب هذا الزائر معطّل."),
            DenialReasonCode.HolderLocked =>
                ("This visitor's account is locked.",
                 "حساب هذا الزائر مقفل."),
            DenialReasonCode.ProfileTypeInactive =>
                ("This visitor's profile type is no longer active.",
                 "نوع ملف هذا الزائر لم يعد نشطاً."),
            DenialReasonCode.OutsideTimeWindow =>
                ("This gate is closed at this time.",
                 "هذه البوابة مغلقة في هذا الوقت."),
            DenialReasonCode.ProfileTypeNotAllowed =>
                ("This gate is not open to this visitor's profile type.",
                 "هذه البوابة ليست مفتوحة لنوع ملف هذا الزائر."),
            DenialReasonCode.BookingRequiredMissing =>
                ("A booking is required for this gate.",
                 "يتطلب هذه البوابة وجود حجز مسبق."),
            _ => ("This scan was denied.", "تم رفض هذا المسح."),
        };

    private static string HashRequest(Guid gateId, GateScanRequest request, string idempotencyKey) =>
        OpaqueToken.Hash(
            $"{gateId:N}|{idempotencyKey}|{request.Qr}|{request.Source}|{request.RequestedDirection}");

    /// <summary>The bundle of holder-derived display fields a denial response
    /// echoes back. Built once from a <see cref="QrResolution"/>; passed to
    /// every denial path so the response payload is consistent.</summary>
    private readonly struct DenialContext(
        Guid? userProfileId, string? displayName, string? displayNameArabic,
        Guid? profileTypeId, string? profileTypeName, string? profileTypePageColor)
    {
        public static readonly DenialContext Empty = default;

        public Guid? UserProfileId { get; } = userProfileId;
        public string? DisplayName { get; } = displayName;
        public string? DisplayNameArabic { get; } = displayNameArabic;
        public Guid? ProfileTypeId { get; } = profileTypeId;
        public string? ProfileTypeName { get; } = profileTypeName;
        public string? ProfileTypePageColor { get; } = profileTypePageColor;

        public static DenialContext From(QrResolution r) =>
            new(r.UserProfileId, r.DisplayName, r.DisplayNameArabic,
                r.ProfileTypeId, r.ProfileTypeName, r.ProfileTypePageColor);

        public GateScanUserProfile? ToProfile() =>
            UserProfileId is null
                ? null
                : new GateScanUserProfile(
                    UserProfileId.Value, DisplayName ?? string.Empty,
                    DisplayNameArabic ?? string.Empty, ProfileTypeId,
                    ProfileTypeName, ProfileTypePageColor);
    }
}
