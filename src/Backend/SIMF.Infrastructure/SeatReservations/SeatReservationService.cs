// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
// Tests: SIMF.Api.Tests/SeatTierEligibilityTests.cs
// Tests: SIMF.Api.Tests/SeatChangeTests.cs (B1 — the atomic seat move)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Notifications;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SeatReservations;

/// <summary>D-175 (gap doc G11, Mockup page 7) — per-session seat
/// reservation orchestration. Hall stays frozen — layout columns
/// live on <c>HallSeatLayout</c>. Active uniqueness is enforced by
/// filtered unique indexes; release sets <c>ReleasedAt</c> and frees
/// the slot for re-booking. Per-session capacity = layout total
/// (rows*seats), further capped by the smaller of
/// <c>Session.CapacityOverride</c> and <c>Hall.Capacity</c>.</summary>
internal sealed class SeatReservationService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    INotificationDispatcher notifications,
    TimeProvider timeProvider,
    ILogger<SeatReservationService> logger) : ISeatReservationService
{
    /// <summary>#6/#17 (owner 2026-07-20) — how long before a session starts an
    /// un-checked-in seat reservation is auto-released so the seat can go to
    /// someone else ("cancelled if you don't check in 3 minutes before start").
    /// A reservation's <c>Expires</c> is stamped at <c>Start - NoShowReleaseGrace</c>;
    /// the no-show release scan reads it. Defined here (not on the worker) so the
    /// stamp written at creation and the scan that reads it share one source.</summary>
    internal static readonly TimeSpan NoShowReleaseGrace = TimeSpan.FromMinutes(3);

    public async Task<SessionSeatMap> GetSessionSeatMapAsync(
        Guid sessionId, Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var layout = await LoadLayoutAsync(session.HallId, cancellationToken);
        var rowLabels = ParseRowLabels(layout?.RowLabels);

        var reservations = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null)
            .Select(r => new
            {
                r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.ReservedForUserId,
                // D-572 — carry the booking status so MyCell can drive the app's
                // seat-card hint (Pending → await approval / Approved → show badge).
                r.Status,
                // D-771 — the admin-typed VVIP guest hint travels with the cell so a
                // protocol seat shows "reserved for the Minister" instead of a bare
                // blocked square.
                r.GuestHint, r.GuestHintArabic,
            })
            .ToListAsync(cancellationToken);

        // Wave 2 — the "confirmed" (تم التأكيد) seat state: a reservation whose
        // holder has an OPEN HallAttendance row for this session (scanned in at the
        // hall gate). One query for the whole session, matched by holder id.
        var checkedInUserIds = (await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.Leave == null)
            .Select(a => a.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var cells = reservations.Select(r => new SessionSeatCell(
            r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
            r.ReservedForUserId is { } holder && checkedInUserIds.Contains(holder),
            r.GuestHint, r.GuestHintArabic))
            .ToList();

        SessionSeatCell? mine = null;
        if (actorUserId is { } actor)
        {
            var ownRow = reservations.FirstOrDefault(r => r.ReservedForUserId == actor);
            if (ownRow is not null)
            {
                mine = new SessionSeatCell(
                    ownRow.Id, ownRow.RowLabel, ownRow.SeatNumber, ownRow.Kind,
                    ownRow.Status,
                    ownRow.ReservedForUserId is { } m && checkedInUserIds.Contains(m),
                    ownRow.GuestHint, ownRow.GuestHintArabic);
            }
        }

        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => new { h.Capacity, h.SeatSelectionMode })
            .SingleAsync(cancellationToken);
        // D-706 — a hall/session with no seat layout has no assignable seats, so it
        // is inherently open seating (a one-tap join); otherwise honour the session
        // override, else the hall's configured mode. Without this, a seeded session
        // (AssignedSeat default, no layout) opened an empty seat picker — the "join
        // not working" the owner reported.
        var hasLayout = rowLabels.Count > 0 && (layout?.SeatsPerRow ?? 0) > 0;
        var effectiveMode = EffectiveMode(
            session.SeatSelectionModeOverride, hall.SeatSelectionMode, hasLayout);

        // D-767 — emit the per-row counts only for a ragged layout; null (key omitted)
        // for a uniform one keeps the shipped wire identical for old and new apps, which
        // then render every row at the still-emitted seatsPerRow (= max for a ragged layout).
        var seatCounts = string.IsNullOrWhiteSpace(layout?.SeatCounts)
            ? null
            : ExpandSeatCounts(layout, rowLabels);

        // D-771 — always emit ONE tier per row (Normal for a pre-D-771 layout) so the
        // app can colour the grid and pre-disable ineligible seats, plus whether the
        // CALLER is VIP-tier. Both are UX hints — the reserve paths re-check.
        var seatTiers = layout is null
            ? Array.Empty<SeatTier>()
            : ExpandSeatTiers(layout, rowLabels);
        var callerIsVip = actorUserId is { } tierActor
            && await IsVipVisitorAsync(tierActor, cancellationToken);

        return new SessionSeatMap(
            sessionId, session.HallId, hall.Capacity, session.CapacityOverride,
            rowLabels, layout?.SeatsPerRow ?? 0,
            cells, mine, cells.Count,
            // D-432 — the session title is already loaded in the snapshot.
            session.Title, session.TitleArabic,
            // D-485 — the effective mode drives the app's Join CTA.
            effectiveMode,
            // D-767 — the ragged per-row counts (null = uniform).
            seatCounts,
            // D-771 — the per-row tiers + the caller's own VIP tier.
            seatTiers, callerIsVip);
    }

    /// <summary>D-706 — the mode the app branches its Join CTA on. A session with
    /// no seat layout has no assignable seats, so it is inherently
    /// <see cref="SeatSelectionMode.OpenSeating"/> (a one-tap join) whatever the
    /// hall's configured mode says; a laid-out session honours the session override,
    /// else the hall's mode. Kept in one place so the seat-map read and the
    /// open-seating join can never disagree.</summary>
    private static SeatSelectionMode EffectiveMode(
        SeatSelectionMode? sessionOverride, SeatSelectionMode hallMode, bool hasLayout) =>
        hasLayout
            ? (sessionOverride ?? hallMode)
            : SeatSelectionMode.OpenSeating;

    public async Task<MySeatReservation> ReserveAsync(
        Guid sessionId, Guid actorUserId,
        ReserveSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var seat = request.SeatNumber;
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        EnsureSeatPickAllowed(ctx);
        EnsureSessionNotEnded(ctx.End);
        ValidateSeatBounds(ctx, row, seat);
        // D-771 — the seat TIER gate, enforced on the SERVER (the app only greys the
        // ineligible seats out): a VVIP seat is never self-reservable, a VIP seat
        // needs the VIP tier, a Normal seat is open to everyone.
        EnsureTierEligible(
            ctx.SeatTiers[RowIndex(ctx.RowLabels, row)],
            await IsVipVisitorAsync(actorUserId, cancellationToken));
        await EnsureSessionHasCapacityAsync(ctx, cancellationToken);

        var existing = await GetMyActiveAsync(sessionId, actorUserId, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyOwnedBySession, 409,
                "You already have a seat reserved for this session.",
                "لديك مقعد محجوز بالفعل لهذه الجلسة.");
        }

        await EnsureNoOverlapAsync(
            sessionId, actorUserId, ctx.Start, ctx.End, cancellationToken);

        var clash = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId
                && r.RowLabel == row
                && r.SeatNumber == seat
                && r.ReleasedAt == null)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (clash != Guid.Empty)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyReserved, 409,
                "That seat is already reserved.",
                "هذا المقعد محجوز بالفعل.");
        }

        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = SeatReservationKind.UserBooking,
            ReservedForUserId = actorUserId,
            CreatedByUserId = actorUserId,
            CreatedAt = timeProvider.GetUtcNow(),
            // 2026-07-18 (reservation-only) — there is no Control Panel approval
            // step: the reservation is confirmed the moment it is made. It stays a
            // provisional hold until the visitor checks in at the hall gate; the
            // pre-start sweep releases any hold that never checks in.
            Status = BookingStatus.Approved,
            // #6/#17 — the no-show release deadline: 3 minutes before the session
            // starts. If the holder has not checked in by then the seat is freed.
            Expires = ctx.Start - NoShowReleaseGrace,
        };
        await PersistWithUniquenessGuardAsync(reservation, cancellationToken);
        // M-2 — hard capacity backstop against a concurrent booking that raced the
        // pre-count; on overflow removes our row and throws SeatSessionFull.
        await EnforceCapacityAfterInsertAsync(
            reservation, EffectiveCapacity(ctx), cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={row}; seat={seat}; kind=UserBooking; status=Approved",
        }, cancellationToken);

        logger.LogInformation(
            "Seat {Row}{Seat} reserved (auto-confirmed) on session {SessionId} by user {Actor}",
            row, seat, sessionId, actorUserId);

        // 2026-07-18 (reservation-only) — no approval step, so nothing is
        // dispatched here; the app shows the reserve-success message inline.
        return ToMine(reservation);
    }

    public async Task<MySeatReservation> ReserveRandomAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        EnsureSeatPickAllowed(ctx);
        EnsureSessionNotEnded(ctx.End);

        var existing = await GetMyActiveAsync(sessionId, actorUserId, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyOwnedBySession, 409,
                "You already have a seat reserved for this session.",
                "لديك مقعد محجوز بالفعل لهذه الجلسة.");
        }

        await EnsureNoOverlapAsync(
            sessionId, actorUserId, ctx.Start, ctx.End, cancellationToken);

        // M-2 / #21 — the capacity COUNT, the free-seat pick and the INSERT run in
        // ONE Serializable transaction so concurrent reserve-random can neither
        // oversell (the key-range lock serialises count-then-insert) nor over-reject
        // (a deadlock victim re-runs and its re-count sees the committed rival),
        // filling exactly the declared capacity. See InsertHoldWithinCapacityAsync.
        var now = timeProvider.GetUtcNow();
        // D-771 — the auto-pick must respect the same tier rule as the self-pick, so
        // resolve the caller's VIP tier ONCE (outside the serializable transaction —
        // it is a read of admin-curated profile data, not of the seat state).
        var callerIsVip = await IsVipVisitorAsync(actorUserId, cancellationToken);
        var reservation = await InsertHoldWithinCapacityAsync(
            sessionId, EffectiveCapacity(ctx),
            async ct =>
            {
                var taken = await LoadHeldSeatsAsync(sessionId, ct);
                return PickRandomSeat(ctx, taken, actorUserId, now, callerIsVip);
            },
            cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={reservation.RowLabel}; seat={reservation.SeatNumber}; "
                + "kind=RandomAssignment; status=Approved",
        }, cancellationToken);

        // 2026-07-18 (reservation-only) — auto-confirmed on create; nothing dispatched here.
        return ToMine(reservation);
    }

    public async Task<MySeatReservation> JoinOpenSeatingAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => new { h.Capacity, h.SeatSelectionMode })
            .SingleAsync(cancellationToken);
        // D-706 — resolve the effective mode with the no-layout rule so a session
        // that has no seat layout accepts this open-seating join (there are no
        // seats to pick); a laid-out assigned-seat session still requires a pick.
        var layout = await LoadLayoutAsync(session.HallId, cancellationToken);
        var hasLayout = ParseRowLabels(layout?.RowLabels).Count > 0
            && (layout?.SeatsPerRow ?? 0) > 0;
        var mode = EffectiveMode(
            session.SeatSelectionModeOverride, hall.SeatSelectionMode, hasLayout);
        if (mode != SeatSelectionMode.OpenSeating)
        {
            throw new ApiException(
                ErrorCodes.SeatSelectionRequired, 409,
                "This session requires you to pick a specific seat.",
                "تتطلب هذه الجلسة اختيار مقعد محدد.");
        }

        EnsureSessionNotEnded(session.End);

        var existing = await GetMyActiveAsync(sessionId, actorUserId, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyOwnedBySession, 409,
                "You already have a booking for this session.",
                "لديك حجز بالفعل لهذه الجلسة.");
        }

        await EnsureNoOverlapAsync(
            sessionId, actorUserId, session.Start, session.End, cancellationToken);

        // M-1 / #21 — open-seating capacity = the session override, else the hall
        // capacity (no seat layout bounds it), and there is NO per-seat DB backstop.
        // So the capacity COUNT and the INSERT run in ONE Serializable transaction
        // (via the execution strategy so it composes with EnableRetryOnFailure):
        // concurrent joins can neither oversell — the key-range lock serialises
        // count-then-insert — nor over-reject. See InsertHoldWithinCapacityAsync.
        var declaredCap = session.CapacityOverride ?? hall.Capacity;
        var now = timeProvider.GetUtcNow();
        var reservation = await InsertHoldWithinCapacityAsync(
            sessionId, declaredCap,
            _ => Task.FromResult<SeatReservation?>(new SeatReservation
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                RowLabel = null,
                SeatNumber = null,
                Kind = SeatReservationKind.OpenSeating,
                ReservedForUserId = actorUserId,
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                // 2026-07-18 (reservation-only) — confirmed on create, no approval
                // step; the hold stays provisional until hall check-in.
                Status = BookingStatus.Approved,
                // #6/#17 — the no-show release deadline: 3 minutes before start.
                Expires = session.Start - NoShowReleaseGrace,
            }),
            cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + "kind=OpenSeating; status=Approved",
        }, cancellationToken);

        logger.LogInformation(
            "Open-seating join (auto-confirmed) on session {SessionId} by user {Actor}",
            sessionId, actorUserId);

        return ToMine(reservation);
    }

    public async Task<MySeatReservation> MoveAsync(
        Guid sessionId, Guid actorUserId,
        MoveSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var seat = request.SeatNumber;
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        EnsureSeatPickAllowed(ctx);
        // B1 (owner rule, deliberate) — a self-service change of seat is allowed only
        // BEFORE the session starts, the SAME boundary the cancel already enforces
        // (D-227 / FR-504). Once the session is running the seat plan is what the
        // staff seating desk and the gate flow work from on the floor, and the
        // pre-start no-show sweep has already redistributed the un-checked-in holds;
        // a visitor reshuffling themselves at that point would desync the desk. A
        // move during a live session goes through staff, not the app.
        EnsureSessionNotStarted(ctx.Start);
        ValidateSeatBounds(ctx, row, seat);
        // D-771 — the DESTINATION seat must pass exactly the same tier gate as a
        // first reservation, via the one shared rule: a VVIP seat is never
        // self-reservable, a VIP seat needs the VIP tier.
        EnsureTierEligible(
            ctx.SeatTiers[RowIndex(ctx.RowLabels, row)],
            await IsVipVisitorAsync(actorUserId, cancellationToken));

        var moved = await MoveHoldAtomicallyAsync(
            ctx, actorUserId, row, seat, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationMoved,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={moved.Reservation.Id}; sessionId={sessionId}; "
                + $"fromRow={moved.FromRowLabel}; fromSeat={moved.FromSeatNumber}; "
                + $"row={row}; seat={seat}; kind=UserBooking; status=Approved",
        }, cancellationToken);

        logger.LogInformation(
            "Seat moved from {FromRow}{FromSeat} to {Row}{Seat} on session {SessionId} by user {Actor}",
            moved.FromRowLabel, moved.FromSeatNumber, row, seat, sessionId, actorUserId);

        return ToMine(moved.Reservation);
    }

    /// <summary>B1 — the ATOMIC half of the seat change: release the held seat and
    /// acquire the destination in ONE serializable transaction, so the holder can
    /// never end up with no seat. Shaped like
    /// <see cref="InsertHoldWithinCapacityAsync"/> — run through the EF execution
    /// strategy (a manual transaction under <c>EnableRetryOnFailure</c> throws
    /// otherwise) and deliberately NOT swallowing <see cref="DbUpdateException"/>, so
    /// a deadlock victim re-runs the whole unit instead of being reported as a taken
    /// seat. The "is the destination free?" read happens INSIDE the transaction, where
    /// its key-range lock stops a concurrent hold slipping in between the read and
    /// the insert; the filtered unique index on (SessionId, RowLabel, SeatNumber) is
    /// the backstop, and firing it rolls the release back with it.
    /// <para>The release is saved BEFORE the insert (two saves, one transaction)
    /// because the OTHER filtered unique index — one active row per
    /// (SessionId, ReservedForUserId) — would otherwise reject the new row while the
    /// old one is still held; statement order inside a single SaveChanges batch is
    /// an EF implementation detail, so it is made explicit here.</para></summary>
    private async Task<(SeatReservation Reservation, string? FromRowLabel, int? FromSeatNumber)>
        MoveHoldAtomicallyAsync(
            SessionContext ctx, Guid actorUserId, string row, int seat,
            CancellationToken cancellationToken)
    {
        SeatReservation? origin = null;
        SeatReservation? added = null;
        (SeatReservation Reservation, string? FromRowLabel, int? FromSeatNumber)? committed = null;
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A retry re-enters here; drop everything a rolled-back attempt left
            // tracked so the next SaveChanges neither re-inserts a stale row nor
            // re-applies a release the database has already thrown away.
            if (added is not null)
            {
                appDbContext.Entry(added).State = EntityState.Detached;
                added = null;
            }
            if (origin is not null)
            {
                appDbContext.Entry(origin).State = EntityState.Detached;
                origin = null;
            }
            committed = null;

            await using var tx = await appDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            origin = await appDbContext.SeatReservations
                .SingleOrDefaultAsync(r => r.SessionId == ctx.SessionId
                    && r.ReservedForUserId == actorUserId
                    && r.ReleasedAt == null, cancellationToken)
                ?? throw new ApiException(
                    ErrorCodes.SeatReservationNotFound, 404,
                    "You do not have a seat to change in this session.",
                    "ليس لديك مقعد لتغييره في هذه الجلسة.");

            if (string.Equals(origin.RowLabel, row, StringComparison.OrdinalIgnoreCase)
                && origin.SeatNumber == seat)
            {
                throw new ApiException(
                    ErrorCodes.SeatMoveSameSeat, 409,
                    "You already have that seat — pick a different one.",
                    "هذا المقعد محجوز لك بالفعل — اختر مقعداً آخر.");
            }

            var taken = await appDbContext.SeatReservations.AsNoTracking()
                .AnyAsync(r => r.SessionId == ctx.SessionId
                    && r.RowLabel == row
                    && r.SeatNumber == seat
                    && r.ReleasedAt == null, cancellationToken);
            if (taken)
            {
                throw new ApiException(
                    ErrorCodes.SeatAlreadyReserved, 409,
                    "That seat is already reserved.",
                    "هذا المقعد محجوز بالفعل.");
            }

            var now = timeProvider.GetUtcNow();
            var fromRow = origin.RowLabel;
            var fromSeat = origin.SeatNumber;
            origin.ReleasedAt = now;
            origin.Status = BookingStatus.Cancelled;
            await appDbContext.SaveChangesAsync(cancellationToken);

            var target = new SeatReservation
            {
                Id = Guid.NewGuid(),
                SessionId = ctx.SessionId,
                RowLabel = row,
                SeatNumber = seat,
                // A move is a deliberate self-pick, whatever the seat it replaces was
                // acquired as (self-pick or auto-pick).
                Kind = SeatReservationKind.UserBooking,
                ReservedForUserId = actorUserId,
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                Status = BookingStatus.Approved,
                // #6/#17 — the moved hold keeps the SAME no-show deadline as the seat
                // it replaces: 3 minutes before the session starts.
                Expires = ctx.Start - NoShowReleaseGrace,
            };
            appDbContext.SeatReservations.Add(target);
            added = target;
            await appDbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            committed = (target, fromRow, fromSeat);
        });

        return committed ?? throw new InvalidOperationException(
            "The seat move completed without producing a reservation.");
    }

    public async Task ReleaseMineAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var mine = await appDbContext.SeatReservations
            .Where(r => r.SessionId == sessionId
                && r.ReservedForUserId == actorUserId
                && r.ReleasedAt == null)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SeatReservationNotFound, 404,
                "You do not have a seat to release in this session.",
                "ليس لديك مقعد للإلغاء في هذه الجلسة.");

        // P2.2 — D-227 (FDS-005 §5.3, FR-504): a booking can only be cancelled
        // BEFORE the session starts.
        var sessionStart = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => (DateTimeOffset?)s.Start)
            .SingleOrDefaultAsync(cancellationToken);
        if (sessionStart is { } start && timeProvider.GetUtcNow() >= start)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionStarted, 409,
                "You cannot cancel a booking after the session has started.",
                "لا يمكنك إلغاء الحجز بعد بدء الجلسة.");
        }

        var now = timeProvider.GetUtcNow();
        mine.ReleasedAt = now;
        mine.Status = BookingStatus.Cancelled;
        await appDbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BookingCancelled,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={mine.Id}; sessionId={sessionId}; "
                + $"row={mine.RowLabel}; seat={mine.SeatNumber}; kind={mine.Kind}",
        }, cancellationToken);
    }

    public async Task<HallSeatLayoutSnapshot> GetLayoutAsync(
        Guid hallId, CancellationToken cancellationToken = default)
    {
        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == hallId)
            .Select(h => new { h.Id, h.Capacity })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.HallNotFound, 404,
                "Hall not found.",
                "لم يتم العثور على القاعة.");
        var layout = await LoadLayoutAsync(hallId, cancellationToken);
        var rowLabels = ParseRowLabels(layout?.RowLabels);
        var seatsPerRow = layout?.SeatsPerRow ?? 0;
        // D-767 — the expanded per-row counts drive the capacity (sum), matching
        // rows × seatsPerRow when uniform; the wire field stays null when uniform so the
        // CP editor reads back exactly what it wrote.
        var expanded = layout is null
            ? Array.Empty<int>()
            : ExpandSeatCounts(layout, rowLabels);
        var seatCounts = string.IsNullOrWhiteSpace(layout?.SeatCounts) ? null : expanded;
        // D-771 — the editor always reads back one tier per row (Normal for a legacy
        // layout), so the CP tier selects render without a special "unset" case.
        var seatTiers = layout is null
            ? Array.Empty<SeatTier>()
            : ExpandSeatTiers(layout, rowLabels);
        return new HallSeatLayoutSnapshot(
            hallId, rowLabels, seatsPerRow,
            expanded.Sum(), hall.Capacity, seatCounts, seatTiers);
    }

    public async Task<HallSeatLayoutSnapshot> SetLayoutAsync(
        Guid actorUserId, Guid hallId,
        SetHallSeatLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var hall = await appDbContext.Halls
            .Where(h => h.Id == hallId)
            .Select(h => new { h.Id, h.Capacity })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.HallNotFound, 404,
                "Hall not found.",
                "لم يتم العثور على القاعة.");

        var rawRows = request.RowLabels ?? Array.Empty<string>();
        var rows = rawRows
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .ToList();
        if (rows.Count is < 1 or > 26
            || rows.Any(r => r.Length > 8)
            || rows.Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count)
        {
            throw new ApiException(
                ErrorCodes.SeatLayoutInvalid, 400,
                "Row labels must be 1–26 unique entries of 1–8 chars each.",
                "يجب أن تكون رموز الصفوف بين 1 و 26 إدخالاً فريداً بطول 1 إلى 8 محارف.");
        }
        // D-767 — resolve the per-row seat counts. When SeatCounts is supplied it is
        // AUTHORITATIVE (a ragged grid, one count per row); when null/empty the layout
        // stays UNIFORM on the frozen SeatsPerRow. Both branches produce one concrete
        // per-row array so capacity, the orphan guard and the persisted CSV agree.
        var requestedCounts = request.SeatCounts ?? Array.Empty<int>();
        var variable = requestedCounts.Count > 0;
        List<int> seatCounts;
        if (variable)
        {
            if (requestedCounts.Count != rows.Count)
            {
                throw new ApiException(
                    ErrorCodes.SeatLayoutInvalid, 400,
                    $"Seat counts ({requestedCounts.Count}) must match the number of rows ({rows.Count}).",
                    $"يجب أن يساوي عدد قيم المقاعد ({requestedCounts.Count}) عدد الصفوف ({rows.Count}).");
            }
            if (requestedCounts.Any(c => c is < 1 or > 80))
            {
                throw new ApiException(
                    ErrorCodes.SeatLayoutInvalid, 400,
                    "Each row's seat count must be between 1 and 80.",
                    "يجب أن يكون عدد مقاعد كل صف بين 1 و 80.");
            }
            seatCounts = requestedCounts.ToList();
        }
        else
        {
            if (request.SeatsPerRow is < 1 or > 80)
            {
                throw new ApiException(
                    ErrorCodes.SeatLayoutInvalid, 400,
                    "Seats per row must be between 1 and 80.",
                    "يجب أن يكون عدد المقاعد في كل صف بين 1 و 80.");
            }
            seatCounts = Enumerable.Repeat(request.SeatsPerRow, rows.Count).ToList();
        }

        var layoutCapacity = seatCounts.Sum();
        if (layoutCapacity > hall.Capacity)
        {
            throw new ApiException(
                ErrorCodes.SeatCapacityExceeded, 400,
                $"Layout capacity ({layoutCapacity}) exceeds hall capacity ({hall.Capacity}).",
                $"السعة المقترحة ({layoutCapacity}) تتجاوز سعة القاعة ({hall.Capacity}).");
        }

        // The persisted uniform fallback: max(counts) for a variable layout (never hides
        // a real seat from an old/uniform reader), else the supplied SeatsPerRow. The
        // CSV is null for a uniform layout so a round-trip reads back exactly what it wrote.
        var seatsPerRow = variable ? seatCounts.Max() : request.SeatsPerRow;
        var countsCsv = variable ? string.Join(',', seatCounts) : null;

        var layout = await appDbContext.HallSeatLayouts
            .SingleOrDefaultAsync(l => l.HallId == hallId, cancellationToken);

        // D-771 — resolve the per-row seat TIERS (owner 2026-07-26). Supplied →
        // AUTHORITATIVE (one defined tier per row). Omitted on an EXISTING layout →
        // keep what is stored, so an older client cannot silently wipe the tiers.
        // Omitted when DEFINING a layout for the first time → every row defaults to
        // VVIP-reserved, per the owner's rule; the admin then downgrades rows.
        var requestedTiers = request.SeatTiers ?? Array.Empty<SeatTier>();
        List<SeatTier> seatTiers;
        if (requestedTiers.Count > 0)
        {
            if (requestedTiers.Count != rows.Count)
            {
                throw new ApiException(
                    ErrorCodes.SeatLayoutInvalid, 400,
                    $"Seat tiers ({requestedTiers.Count}) must match the number of rows ({rows.Count}).",
                    $"يجب أن يساوي عدد فئات المقاعد ({requestedTiers.Count}) عدد الصفوف ({rows.Count}).");
            }
            if (requestedTiers.Any(t => !Enum.IsDefined(typeof(SeatTier), t)))
            {
                throw new ApiException(
                    ErrorCodes.SeatLayoutInvalid, 400,
                    "Each row's seat tier must be Normal, VIP or VVIP.",
                    "يجب أن تكون فئة كل صف: عادي أو كبار الشخصيات أو شخصيات بالغة الأهمية.");
            }
            seatTiers = requestedTiers.ToList();
        }
        else if (layout is null)
        {
            seatTiers = Enumerable.Repeat(SeatTier.Vvip, rows.Count).ToList();
        }
        else
        {
            // Keep the stored tiers, re-aligned POSITIONALLY to the new row set (a
            // row added at the end inherits the owner's VVIP default).
            var stored = ExpandSeatTiers(layout, ParseRowLabels(layout.RowLabels));
            seatTiers = Enumerable.Range(0, rows.Count)
                .Select(i => i < stored.Count ? stored[i] : SeatTier.Vvip)
                .ToList();
        }
        var tiersCsv = string.Join(',', seatTiers.Select(t => (int)t));

        // H-2 — an existing layout may already back active reservations; a change
        // that drops a row or shrinks a row's seat count would strand any seat that
        // now falls outside the grid. Block it (the operator must release those
        // seats first). A first-time layout (layout is null) can have no seat-
        // specific reservations yet — the reserve paths require a layout.
        if (layout is not null)
        {
            await EnsureLayoutChangeKeepsActiveReservationsAsync(
                hallId, rows, seatCounts, cancellationToken);
        }
        var now = timeProvider.GetUtcNow();
        var rowsCsv = string.Join(',', rows);
        if (layout is null)
        {
            layout = new HallSeatLayout
            {
                Id = Guid.NewGuid(),
                HallId = hallId,
                RowLabels = rowsCsv,
                SeatsPerRow = seatsPerRow,
                SeatCounts = countsCsv,
                SeatTiers = tiersCsv,
                CreatedAt = now,
            };
            appDbContext.HallSeatLayouts.Add(layout);
        }
        else
        {
            layout.RowLabels = rowsCsv;
            layout.SeatsPerRow = seatsPerRow;
            layout.SeatCounts = countsCsv;
            layout.SeatTiers = tiersCsv;
            layout.UpdatedAt = now;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallSeatLayoutUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"hallId={hallId}; rows={rowsCsv}; seatsPerRow={seatsPerRow}; "
                + $"seatCounts={countsCsv ?? "(uniform)"}; seatTiers={tiersCsv}",
        }, cancellationToken);

        return new HallSeatLayoutSnapshot(
            hallId, rows, seatsPerRow, layoutCapacity, hall.Capacity,
            variable ? seatCounts : null, seatTiers);
    }

    public async Task AdminReserveRowAsync(
        Guid actorUserId, Guid sessionId,
        AdminReserveRowRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        // D-767 — resolve the row's index so the block fills exactly THIS row's seat
        // count (ctx.SeatCounts[rowIndex]) rather than a single uniform width.
        var rowIndex = RowIndex(ctx.RowLabels, row);
        if (rowIndex < 0)
        {
            throw new ApiException(
                ErrorCodes.SeatOutOfBounds, 400,
                $"Row '{row}' is not in the hall layout.",
                $"الصف '{row}' غير موجود في مخطط القاعة.");
        }

        var occupiedInRow = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId
                && r.RowLabel == row
                && r.ReleasedAt == null)
            .Select(r => r.SeatNumber)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<int>(
            occupiedInRow.Where(s => s.HasValue).Select(s => s!.Value));

        var now = timeProvider.GetUtcNow();
        var inserted = 0;
        for (var seat = 1; seat <= ctx.SeatCounts[rowIndex]; seat++)
        {
            if (taken.Contains(seat)) continue;
            var reservation = new SeatReservation
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                RowLabel = row,
                SeatNumber = seat,
                Kind = SeatReservationKind.AdminReservedRow,
                ReservedForUserId = null,
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                // P2.2 — D-227: an admin block is not a visitor booking; it is
                // confirmed immediately and never enters the approval queue.
                Status = BookingStatus.Approved,
            };
            try
            {
                await PersistWithUniquenessGuardAsync(reservation, cancellationToken);
                inserted++;
            }
            catch (ApiException ex) when (ex.Code == ErrorCodes.SeatAlreadyReserved)
            {
                // Lost a race against a concurrent self-pick — fine.
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatRowAdminReserved,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; row={row}; inserted={inserted}",
        }, cancellationToken);
    }

    // 2026-07-18 — reserve ONE specific seat for a VIP: a single admin block on
    // that seat (Kind=AdminReservedRow, no attendee), confirmed immediately so it
    // never enters the (dormant) approval queue. Released like any admin block.
    public async Task AdminReserveSeatAsync(
        Guid actorUserId, Guid sessionId,
        AdminReserveSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var seat = request.SeatNumber;
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        ValidateSeatBounds(ctx, row, seat);

        var clash = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId
                && r.RowLabel == row
                && r.SeatNumber == seat
                && r.ReleasedAt == null)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (clash != Guid.Empty)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyReserved, 409,
                "That seat is already reserved.",
                "هذا المقعد محجوز بالفعل.");
        }

        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = SeatReservationKind.AdminReservedRow,
            ReservedForUserId = null,
            CreatedByUserId = actorUserId,
            CreatedAt = timeProvider.GetUtcNow(),
            // An admin block is confirmed immediately (never enters the queue).
            Status = BookingStatus.Approved,
            // D-771 — the manual guest hint. A VVIP seat has no registration, so this
            // free text IS the occupant record the app + the staff seating desk read.
            GuestHint = NormaliseHint(request.GuestHint),
            GuestHintArabic = NormaliseHint(request.GuestHintArabic),
        };
        await PersistWithUniquenessGuardAsync(reservation, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatRowAdminReserved,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; row={row}; seat={seat}; single=true; "
                + "guestHint="
                + ((reservation.GuestHint ?? reservation.GuestHintArabic) is null
                    ? "(none)" : "(set)"),
        }, cancellationToken);
    }

    /// <summary>D-771 — trim the admin-typed guest hint to null-or-content and reject
    /// anything past the persisted 256-char column, so an over-long hint fails as a
    /// caller error rather than a truncation or a DbUpdateException.</summary>
    private static string? NormaliseHint(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }
        if (trimmed.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "The guest note must be 256 characters or fewer.",
                "يجب ألا يتجاوز تنويه الضيف 256 حرفاً.");
        }
        return trimmed;
    }

    public async Task AdminReleaseAsync(
        Guid actorUserId, Guid sessionId, Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await appDbContext.SeatReservations
            .SingleOrDefaultAsync(r => r.Id == reservationId
                && r.SessionId == sessionId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SeatReservationNotFound, 404,
                "Seat reservation not found.",
                "لم يتم العثور على حجز المقعد.");
        if (reservation.ReleasedAt is not null)
        {
            return;
        }
        // M-4 — a release must also close the booking's lifecycle. Leaving Status
        // untouched left an Approved row with ReleasedAt set (a stale
        // "confirmed-but-gone" state the CP/app could still read as active). Mark it
        // Cancelled and stamp the reviewer (the admin performing the release).
        var now = timeProvider.GetUtcNow();
        reservation.ReleasedAt = now;
        reservation.Status = BookingStatus.Cancelled;
        reservation.ReviewedByUserId = actorUserId;
        reservation.ReviewedAt = now;
        await appDbContext.SaveChangesAsync(cancellationToken);

        var eventType = reservation.Kind == SeatReservationKind.AdminReservedRow
            ? AuditEvents.SeatRowAdminReleased
            : AuditEvents.SeatReservationReleased;
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={reservation.RowLabel}; seat={reservation.SeatNumber}; "
                + $"kind={reservation.Kind}",
        }, cancellationToken);

        // M-4 — tell the attendee an admin released their held/confirmed seat
        // (no-op for an AdminReservedRow block: ReservedForUserId is null).
        var session = await LoadSessionTitleAsync(reservation.SessionId, cancellationToken);
        await TryNotifyBookingReleasedAsync(reservation, session, cancellationToken);
    }

    public async Task<GridPage<SessionSeatCell>> ListSessionReservationsAsync(
        Guid sessionId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

        var baseQuery = appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null);
        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderBy(r => r.RowLabel).ThenBy(r => r.SeatNumber)
            .Skip(skip).Take(top)
            .Select(r => new SessionSeatCell(r.Id, r.RowLabel, r.SeatNumber, r.Kind))
            .ToListAsync(cancellationToken);
        return GridPage<SessionSeatCell>.Of(rows, total,
            skip, top);
    }

    // -- Booking monitor + no-show release (#6/#17 — owner 2026-07-20) --

    public async Task<GridPage<ActiveBookingRow>> ListActiveBookingsAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

        // #6 — the read-only Control Panel monitor of ACTIVE (confirmed, still-held)
        // visitor reservations across all sessions. There is no approval step —
        // bookings auto-confirm — so this is a monitor, not a queue. Admin
        // row-blocks are created Approved with a null ReservedForUserId, so they
        // never appear here. The session is joined up-front (before paging) so the
        // session and seat columns are server-filterable/sortable (D-255). The
        // attendee name is resolved cross-DB from Identity afterwards, so that
        // column stays non-filterable/non-sortable (D-157).
        var joined = appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.Status == BookingStatus.Approved
                && r.ReleasedAt == null
                && r.ReservedForUserId != null)
            .Join(appDbContext.Sessions.AsNoTracking(),
                r => r.SessionId, s => s.Id,
                (r, s) => new
                {
                    r.Id, r.SessionId, s.Title, s.TitleArabic, s.Start,
                    r.RowLabel, r.SeatNumber, r.Kind, r.ReservedForUserId, r.CreatedAt,
                });

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "session":
                    joined = joined.Where(x => x.Title.Contains(v) || x.TitleArabic.Contains(v));
                    break;
                case "seat":
                    joined = joined.Where(x => x.RowLabel != null && x.RowLabel.Contains(v));
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: newest booking first.
        joined = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("session", false) => joined.OrderBy(x => x.Title),
            ("session", true) => joined.OrderByDescending(x => x.Title),
            ("start", false) => joined.OrderBy(x => x.Start),
            ("start", true) => joined.OrderByDescending(x => x.Start),
            ("seat", false) => joined.OrderBy(x => x.RowLabel).ThenBy(x => x.SeatNumber),
            ("seat", true) => joined.OrderByDescending(x => x.RowLabel).ThenByDescending(x => x.SeatNumber),
            ("bookedat", false) => joined.OrderBy(x => x.CreatedAt),
            ("bookedat", true) => joined.OrderByDescending(x => x.CreatedAt),
            _ => joined.OrderByDescending(x => x.CreatedAt),
        };

        var total = await joined.CountAsync(cancellationToken);
        var rows = await joined
            .Skip(skip).Take(top)
            .ToListAsync(cancellationToken);

        // Resolve attendee display names in one Identity round-trip (no
        // cross-DB JOIN, D-157).
        var attendeeIds = rows
            .Where(r => r.ReservedForUserId is not null)
            .Select(r => r.ReservedForUserId!.Value)
            .Distinct()
            .ToList();
        var names = attendeeIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await identityDbContext.Users.AsNoTracking()
                .Where(u => attendeeIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => (string?)u.DisplayName, cancellationToken);

        var items = rows.Select(r =>
        {
            string attendeeName = string.Empty;
            if (r.ReservedForUserId is { } uid && names.TryGetValue(uid, out var dn))
            {
                attendeeName = dn ?? string.Empty;
            }
            return new ActiveBookingRow(
                r.Id, r.SessionId, r.Title, r.TitleArabic, r.Start,
                r.RowLabel, r.SeatNumber, r.Kind, r.ReservedForUserId,
                attendeeName, r.CreatedAt);
        }).ToList();

        return GridPage<ActiveBookingRow>.Of(items, total,
            skip, top);
    }

    /// <summary>#6/#17 (owner 2026-07-20, FR-503/903) — the no-show release: free
    /// every ACTIVE (Approved, still-held) visitor seat reservation whose no-show
    /// deadline (<c>Expires</c> = the session's <c>Start − 3min</c>) has passed
    /// <b>and whose holder never checked in</b> (no <c>HallAttendance</c> for that
    /// session — arrival by gate scan or geofence). A walk-in who booked at or after
    /// the deadline (<c>CreatedAt &gt;= Expires</c>) is exempt — they are present,
    /// not a no-show. Each freed holder is notified (<see cref="NotificationKind.BookingReleased"/>).
    /// Admin blocks (null holder) are never touched. Returns the number released.
    /// Called once per minute by <c>ReservationNoShowReleaseWorker</c>; extracted so
    /// the release rule is unit-tested at the service (not by driving the loop).</summary>
    public async Task<int> ReleaseNoShowsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var due = await appDbContext.SeatReservations
            .Where(r => r.Status == BookingStatus.Approved
                && r.ReleasedAt == null
                && r.ReservedForUserId != null
                && r.Expires != null
                && r.Expires <= now
                && r.CreatedAt < r.Expires)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        // One round-trip: every (session, holder) that has ANY check-in row for the
        // sessions in play. The owner rule is "عدم تسجيل الدخول للجلسة" (never checked
        // in), so a holder with any HallAttendance is kept; everyone else is released.
        var sessionIds = due.Select(r => r.SessionId).Distinct().ToList();
        var checkedIn = (await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => sessionIds.Contains(a.SessionId))
            .Select(a => new { a.SessionId, a.UserId })
            .ToListAsync(cancellationToken))
            .Select(a => (a.SessionId, a.UserId))
            .ToHashSet();

        var released = due
            .Where(r => !checkedIn.Contains((r.SessionId, r.ReservedForUserId!.Value)))
            .ToList();
        if (released.Count == 0)
        {
            return 0;
        }
        foreach (var r in released)
        {
            r.ReleasedAt = now;
            r.Status = BookingStatus.Cancelled;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        // Audit + notify each freed no-show. Titles resolved once per session.
        var titles = await appDbContext.Sessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Title, s.TitleArabic })
            .ToDictionaryAsync(
                s => s.Id, s => (s.Title, s.TitleArabic), cancellationToken);
        foreach (var r in released)
        {
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.SeatReservationReleased,
                Outcome = AuditOutcome.Success,
                ActorUserId = null, // system-initiated (the pre-start sweep)
                Detail = $"reservationId={r.Id}; sessionId={r.SessionId}; "
                    + $"row={r.RowLabel}; seat={r.SeatNumber}; reason=no-show",
            }, cancellationToken);
            if (titles.TryGetValue(r.SessionId, out var t))
            {
                await TryNotifyBookingReleasedAsync(r, t, cancellationToken, noShow: true);
            }
        }

        logger.LogInformation(
            "No-show release freed {Count} un-checked-in seat hold(s).", released.Count);
        return released.Count;
    }

    // -- Staff seating desk (D-771 — owner 2026-07-26) --

    public async Task<StaffSeatOccupant> ResolveSeatOccupantAsync(
        Guid sessionId, string rowLabel, int seatNumber,
        CancellationToken cancellationToken = default)
    {
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        var row = (rowLabel ?? string.Empty).Trim();
        ValidateSeatBounds(ctx, row, seatNumber);
        var tier = ctx.SeatTiers[RowIndex(ctx.RowLabels, row)];

        var held = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId
                && r.RowLabel == row
                && r.SeatNumber == seatNumber
                && r.ReleasedAt == null)
            .Select(r => new
            {
                r.Id, r.Kind, r.Status, r.ReservedForUserId,
                r.GuestHint, r.GuestHintArabic,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (held is null)
        {
            // A free seat is a valid answer, not an error: the desk shows "this seat
            // is empty" (and, for a VVIP seat, that it is protocol seating).
            return EmptySeat(sessionId, row, seatNumber, tier);
        }

        var occupant = await LoadOccupantAsync(
            sessionId, held.ReservedForUserId, cancellationToken);
        return new StaffSeatOccupant(
            true, sessionId, row, seatNumber, tier,
            held.Id, held.Kind, held.Status, held.ReservedForUserId,
            occupant.Name, occupant.NameArabic,
            held.GuestHint, held.GuestHintArabic,
            occupant.HasPhoto, occupant.QrId, occupant.CheckedIn);
    }

    public async Task<StaffSeatOccupant> ResolveBadgeSeatAsync(
        Guid sessionId, string qrId, CancellationToken cancellationToken = default)
    {
        var code = (qrId ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "Scan or type a badge code.",
                "امسح رمز البطاقة أو اكتبه.");
        }

        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var layout = await LoadLayoutAsync(session.HallId, cancellationToken);
        var rowLabels = ParseRowLabels(layout?.RowLabels);
        var tiers = layout is null
            ? Array.Empty<SeatTier>()
            : ExpandSeatTiers(layout, rowLabels);

        var holder = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.QrId == code)
            .Select(p => new { p.UserId, p.Name, p.NameArabic })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AttendeeQrUnknown, 404,
                "That badge was not recognised.",
                "لم يتم التعرف على هذه البطاقة.");

        var held = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId
                && r.ReservedForUserId == holder.UserId
                && r.ReleasedAt == null)
            .Select(r => new
            {
                r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
                r.GuestHint, r.GuestHintArabic,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var occupant = await LoadOccupantAsync(
            sessionId, holder.UserId, cancellationToken);
        if (held is null)
        {
            // The badge is valid but the guest holds no seat in this session — the
            // desk shows the "no seat" state with the guest's identity so staff can
            // still help them (Found = false).
            return new StaffSeatOccupant(
                false, sessionId, null, null, SeatTier.Normal,
                null, SeatReservationKind.UserBooking, BookingStatus.Cancelled,
                holder.UserId, occupant.Name, occupant.NameArabic,
                null, null, occupant.HasPhoto, code, occupant.CheckedIn);
        }

        var tierIndex = held.RowLabel is null
            ? -1
            : RowIndex(rowLabels, held.RowLabel);
        var tier = tierIndex >= 0 && tierIndex < tiers.Count
            ? tiers[tierIndex]
            : SeatTier.Normal;
        return new StaffSeatOccupant(
            true, sessionId, held.RowLabel, held.SeatNumber, tier,
            held.Id, held.Kind, held.Status, holder.UserId,
            occupant.Name, occupant.NameArabic,
            held.GuestHint, held.GuestHintArabic,
            occupant.HasPhoto, code, occupant.CheckedIn);
    }

    private static StaffSeatOccupant EmptySeat(
        Guid sessionId, string rowLabel, int seatNumber, SeatTier tier) =>
        new(false, sessionId, rowLabel, seatNumber, tier,
            null, SeatReservationKind.UserBooking, BookingStatus.Cancelled,
            null, string.Empty, string.Empty, null, null, false, null, false);

    /// <summary>D-771 — the occupant facts the seating desk shows: bilingual name +
    /// badge id (from the App-side <c>UserProfile</c>), whether an avatar exists in
    /// the unified file store, and whether they have already checked into this
    /// session. Everything is on the App DB, so there is no cross-database read and
    /// nothing is duplicated (D-157). A null <paramref name="userId"/> (a VVIP
    /// protocol seat or an admin block) yields the empty occupant.</summary>
    private async Task<(string Name, string NameArabic, bool HasPhoto,
        string? QrId, bool CheckedIn)> LoadOccupantAsync(
        Guid sessionId, Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return (string.Empty, string.Empty, false, null, false);
        }
        var profile = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == id)
            .Select(p => new { p.Name, p.NameArabic, p.QrId })
            .FirstOrDefaultAsync(cancellationToken);
        var hasPhoto = await appDbContext.StoredFiles.AsNoTracking()
            .AnyAsync(f => f.Service == FileService.Avatar
                && f.OwnerEntityId == id
                && f.IsActive, cancellationToken);
        var checkedIn = await appDbContext.HallAttendances.AsNoTracking()
            .AnyAsync(a => a.SessionId == sessionId && a.UserId == id,
                cancellationToken);
        return (profile?.Name ?? string.Empty, profile?.NameArabic ?? string.Empty,
            hasPhoto, profile?.QrId, checkedIn);
    }

    // -- internals --

    private Task<(string Title, string TitleArabic)> LoadSessionTitleAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new ValueTuple<string, string>(s.Title, s.TitleArabic))
            .SingleAsync(cancellationToken);

    private async Task EnsureNoOverlapAsync(
        Guid sessionId, Guid actorUserId,
        DateTimeOffset start, DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        // FR-502: the attendee must not already hold a (Pending or Approved)
        // booking for ANOTHER session whose time window overlaps this one.
        // Held = ReleasedAt IS NULL, so released/rejected/cancelled rows don't
        // block.
        var overlaps = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.ReservedForUserId == actorUserId
                && r.ReleasedAt == null
                && r.SessionId != sessionId)
            .Join(appDbContext.Sessions.AsNoTracking(),
                r => r.SessionId, s => s.Id, (r, s) => new { s.Start, s.End })
            .AnyAsync(x => x.Start < end && start < x.End,
                cancellationToken);
        if (overlaps)
        {
            throw new ApiException(
                ErrorCodes.BookingOverlap, 409,
                "You already have a booking for another session at this time.",
                "لديك حجز لجلسة أخرى في نفس الوقت.");
        }
    }

    private async Task<SessionContext> BuildContextAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var layout = await LoadLayoutAsync(session.HallId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SeatLayoutMissing, 400,
                "This hall does not have a seat layout configured.",
                "لا يحتوي هذا المبنى على مخطط مقاعد مُعدّ.");
        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => new { h.Capacity, h.SeatSelectionMode })
            .SingleAsync(cancellationToken);
        var effectiveMode = session.SeatSelectionModeOverride ?? hall.SeatSelectionMode;
        var rowLabels = ParseRowLabels(layout.RowLabels);
        return new SessionContext(
            session.Id, session.HallId, session.CapacityOverride,
            hall.Capacity, layout, rowLabels,
            session.Title, session.TitleArabic, session.Start, session.End,
            effectiveMode, ExpandSeatCounts(layout, rowLabels),
            ExpandSeatTiers(layout, rowLabels));
    }

    private async Task<SessionSnapshot> LoadSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        return await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new SessionSnapshot(
                s.Id, s.HallId, s.CapacityOverride, s.Title, s.TitleArabic,
                s.Start, s.End, s.SeatSelectionModeOverride))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "Session not found.",
                "لم يتم العثور على الجلسة.");
    }

    private Task<HallSeatLayout?> LoadLayoutAsync(
        Guid hallId, CancellationToken cancellationToken)
    {
        return appDbContext.HallSeatLayouts.AsNoTracking()
            .SingleOrDefaultAsync(l => l.HallId == hallId, cancellationToken);
    }

    /// <summary>H-2 — reject a hall-layout change that would orphan any active
    /// (ReleasedAt IS NULL) seat-specific reservation across the hall's sessions:
    /// a booked row no longer in <paramref name="newRows"/>, or (D-767) a seat number
    /// above that row's new per-row count in <paramref name="newSeatCounts"/>.
    /// Open-seating reservations (null row/seat) are unaffected. The operator must
    /// release the affected seats before shrinking the grid.</summary>
    private async Task EnsureLayoutChangeKeepsActiveReservationsAsync(
        Guid hallId, IReadOnlyList<string> newRows, IReadOnlyList<int> newSeatCounts,
        CancellationToken cancellationToken)
    {
        var sessionIds = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.HallId == hallId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (sessionIds.Count == 0)
        {
            return;
        }

        var activeSeats = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => sessionIds.Contains(r.SessionId)
                && r.ReleasedAt == null
                && r.RowLabel != null)
            .Select(r => new { r.RowLabel, r.SeatNumber })
            .ToListAsync(cancellationToken);

        var orphaned = activeSeats.Any(s =>
        {
            var idx = RowIndex(newRows, s.RowLabel!);
            return idx < 0 || (s.SeatNumber ?? 0) > newSeatCounts[idx];
        });
        if (orphaned)
        {
            throw new ApiException(
                ErrorCodes.SeatLayoutHasReservations, 409,
                "This layout change would strand active seat reservations. "
                + "Release the affected seats before changing the layout.",
                "سيؤدي تغيير المخطط إلى إلغاء حجوزات مقاعد نشطة. "
                + "يرجى إلغاء المقاعد المتأثرة قبل تغيير المخطط.");
        }
    }

    private static IReadOnlyList<string> ParseRowLabels(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

    /// <summary>D-767 — expand a layout's per-row seat counts into a concrete array
    /// parallel to <paramref name="rowLabels"/>. When <c>SeatCounts</c> is null/blank the
    /// layout is uniform, so every row gets <c>SeatsPerRow</c> (unchanged pre-D-767
    /// behaviour); when set it is a CSV of ints, one per row. A stored CSV whose length
    /// differs from the row set, or that fails to parse, is corrupt persisted state — a
    /// deterministic 500, never a silent fallback (§2 no-silent-fallback rule).</summary>
    private IReadOnlyList<int> ExpandSeatCounts(
        HallSeatLayout layout, IReadOnlyList<string> rowLabels)
    {
        if (string.IsNullOrWhiteSpace(layout.SeatCounts))
        {
            return Enumerable.Repeat(layout.SeatsPerRow, rowLabels.Count).ToArray();
        }
        var parts = layout.SeatCounts.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var counts = new int[parts.Length];
        var parsedOk = parts.Length == rowLabels.Count;
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out counts[i]))
            {
                parsedOk = false;
            }
        }
        if (!parsedOk)
        {
            logger.LogError(
                "Corrupt HallSeatLayout.SeatCounts '{SeatCounts}' for {RowCount} row(s) on layout {LayoutId}",
                layout.SeatCounts, rowLabels.Count, layout.Id);
            throw new ApiException(
                ErrorCodes.SeatLayoutInvalid, 500,
                "The stored seat layout is invalid.",
                "مخطط المقاعد المُخزَّن غير صالح.");
        }
        return counts;
    }

    /// <summary>D-771 — expand a layout's per-row seat TIERS into a concrete array
    /// parallel to <paramref name="rowLabels"/>. A null/blank <c>SeatTiers</c> is a
    /// layout written before D-771, so every row reads
    /// <see cref="SeatTier.Normal"/> — the exact pre-D-771 behaviour, no shipped
    /// session loses a bookable seat. A stored CSV whose length differs from the row
    /// set, or that fails to parse into a defined tier, is corrupt persisted state —
    /// a deterministic 500, never a silent fallback (§2 no-silent-fallback rule).</summary>
    private IReadOnlyList<SeatTier> ExpandSeatTiers(
        HallSeatLayout layout, IReadOnlyList<string> rowLabels)
    {
        if (string.IsNullOrWhiteSpace(layout.SeatTiers))
        {
            return Enumerable.Repeat(SeatTier.Normal, rowLabels.Count).ToArray();
        }
        var parts = layout.SeatTiers.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tiers = new SeatTier[parts.Length];
        var parsedOk = parts.Length == rowLabels.Count;
        for (var i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out var raw) && Enum.IsDefined(typeof(SeatTier), raw))
            {
                tiers[i] = (SeatTier)raw;
            }
            else
            {
                parsedOk = false;
            }
        }
        if (!parsedOk)
        {
            logger.LogError(
                "Corrupt HallSeatLayout.SeatTiers '{SeatTiers}' for {RowCount} row(s) on layout {LayoutId}",
                layout.SeatTiers, rowLabels.Count, layout.Id);
            throw new ApiException(
                ErrorCodes.SeatLayoutInvalid, 500,
                "The stored seat layout is invalid.",
                "مخطط المقاعد المُخزَّن غير صالح.");
        }
        return tiers;
    }

    /// <summary>D-771 — the eligibility rule, in ONE place so the self-pick, the
    /// random pick and the seat map can never disagree (owner 2026-07-26):
    /// <list type="bullet">
    /// <item><see cref="SeatTier.Vvip"/> — never self-reservable by anyone. There is
    /// no registration for a protocol seat; an administrator blocks it and types the
    /// guest hint.</item>
    /// <item><see cref="SeatTier.Vip"/> — only a VIP-tier visitor (their
    /// <c>ProfileType.AllowsVipMeetingSlots</c>, the seeded VVIP + VIP rows and the
    /// same flag the app already reads as <c>isVip</c>).</item>
    /// <item><see cref="SeatTier.Normal"/> — every visitor type, VIP included.</item>
    /// </list></summary>
    private static bool IsSelfReservable(SeatTier tier, bool callerIsVip) =>
        tier switch
        {
            SeatTier.Vvip => false,
            SeatTier.Vip => callerIsVip,
            _ => true,
        };

    /// <summary>D-771 — throw the caller-facing eligibility error for the seat's
    /// tier. Two distinct codes so the app can explain the refusal precisely: a VVIP
    /// seat is reserved for protocol (nobody may take it), a VIP seat needs the VIP
    /// tier.</summary>
    private static void EnsureTierEligible(SeatTier tier, bool callerIsVip)
    {
        if (IsSelfReservable(tier, callerIsVip))
        {
            return;
        }
        if (tier == SeatTier.Vvip)
        {
            throw new ApiException(
                ErrorCodes.SeatTierReserved, 409,
                "This seat is reserved for protocol guests and cannot be booked.",
                "هذا المقعد محجوز لكبار الضيوف ولا يمكن حجزه.");
        }
        throw new ApiException(
            ErrorCodes.SeatTierNotEligible, 409,
            "This seat is reserved for VIP guests.",
            "هذا المقعد مخصص لكبار الشخصيات.");
    }

    /// <summary>D-771 — is this visitor a VIP-tier attendee? Reuses the EXISTING
    /// VIP-tier notion rather than inventing a parallel one:
    /// <c>UserProfile.ProfileTypeId → UserProfileType.AllowsVipMeetingSlots</c>, which
    /// the seeder sets on the VVIP + VIP audience tiers (D-611) and the app already
    /// surfaces as <c>isVip</c> (D-729). Both tables live on the App DB, so this is a
    /// single local query — no cross-database read.</summary>
    private async Task<bool> IsVipVisitorAsync(
        Guid actorUserId, CancellationToken cancellationToken) =>
        await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId && p.ProfileTypeId != null)
            .Join(appDbContext.ProfileTypes.AsNoTracking(),
                p => p.ProfileTypeId, t => (Guid?)t.Id, (p, t) => t.AllowsVipMeetingSlots)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>D-767 — index of <paramref name="label"/> within
    /// <paramref name="rowLabels"/> (OrdinalIgnoreCase), or -1 when absent. Used to map a
    /// row label onto its per-row seat count in the expanded array.</summary>
    private static int RowIndex(IReadOnlyList<string> rowLabels, string label)
    {
        for (var i = 0; i < rowLabels.Count; i++)
        {
            if (string.Equals(rowLabels[i], label, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static void EnsureSeatPickAllowed(SessionContext ctx)
    {
        // D-485 — the seat-pick paths are only for assigned-seat sessions; an
        // open-seating session is joined via JoinOpenSeatingAsync (no seat).
        if (ctx.EffectiveMode == SeatSelectionMode.OpenSeating)
        {
            throw new ApiException(
                ErrorCodes.OpenSeatingOnly, 409,
                "This session is open seating — just join, no seat to pick.",
                "هذه الجلسة بمقاعد مفتوحة — انضم فقط دون اختيار مقعد.");
        }
    }

    private static void ValidateSeatBounds(
        SessionContext ctx, string rowLabel, int seatNumber)
    {
        // D-767 — bound the seat against THIS row's count (ctx.SeatCounts[i]), not a
        // single uniform width, so a ragged layout accepts/rejects per row.
        var i = RowIndex(ctx.RowLabels, rowLabel ?? string.Empty);
        if (i < 0)
        {
            throw new ApiException(
                ErrorCodes.SeatOutOfBounds, 400,
                $"Row '{rowLabel}' is not in the hall layout.",
                $"الصف '{rowLabel}' غير موجود في مخطط القاعة.");
        }
        if (seatNumber < 1 || seatNumber > ctx.SeatCounts[i])
        {
            throw new ApiException(
                ErrorCodes.SeatOutOfBounds, 400,
                $"Seat number must be between 1 and {ctx.SeatCounts[i]}.",
                $"يجب أن يكون رقم المقعد بين 1 و {ctx.SeatCounts[i]}.");
        }
    }

    private async Task EnsureSessionHasCapacityAsync(
        SessionContext ctx, CancellationToken cancellationToken)
    {
        var active = await appDbContext.SeatReservations
            .Where(r => r.SessionId == ctx.SessionId && r.ReleasedAt == null)
            .CountAsync(cancellationToken);
        if (active >= EffectiveCapacity(ctx))
        {
            throw new ApiException(
                ErrorCodes.SeatSessionFull, 409,
                "No seats remain in this session.",
                "لا توجد مقاعد متبقية في هذه الجلسة.");
        }
    }

    /// <summary>#20 (Round-1 held item, option C) — a booking may still be created on
    /// a live, in-progress session (a walk-in can join), but NOT on one that has
    /// already ENDED: an ended session's seat can never be attended, so the hold would
    /// be dead, un-cancellable weight. Blocks at or after <paramref name="end"/>;
    /// a merely-started (not yet ended) session stays bookable.</summary>
    private void EnsureSessionNotEnded(DateTimeOffset end)
    {
        if (timeProvider.GetUtcNow() >= end)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionEnded, 409,
                "This session has ended; you can no longer book a seat.",
                "انتهت هذه الجلسة، ولم يعد بإمكانك حجز مقعد.");
        }
    }

    /// <summary>B1 — the self-service seat CHANGE window: only BEFORE the session
    /// starts. Deliberately the same boundary <see cref="ReleaseMineAsync"/> uses for
    /// a cancel (D-227 / FR-504) rather than the looser not-yet-ENDED rule the create
    /// paths use: a walk-in may still book a live session, but reshuffling an
    /// already-placed attendee mid-session would desync the staff seating desk and the
    /// pre-start no-show sweep that has already redistributed the free seats.</summary>
    private void EnsureSessionNotStarted(DateTimeOffset start)
    {
        if (timeProvider.GetUtcNow() >= start)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionStarted, 409,
                "You cannot change your seat after the session has started.",
                "لا يمكنك تغيير مقعدك بعد بدء الجلسة.");
        }
    }

    /// <summary>The session's effective place count: the seat-layout total
    /// (D-767: sum of the per-row seat counts — equal to rows × seatsPerRow when the
    /// layout is uniform) capped by the smaller of Session.CapacityOverride and
    /// Hall.Capacity. One definition shared by the reserve pre-check and the
    /// post-insert backstop so they can never disagree.</summary>
    private static int EffectiveCapacity(SessionContext ctx) =>
        Math.Min(
            ctx.SeatCounts.Sum(),
            ctx.CapacityOverride ?? ctx.HallCapacity);

    /// <summary>M-2/M-1 — the hard capacity backstop the pre-count cannot give.
    /// The reserve/join pre-check reads-then-counts-then-inserts, so two
    /// concurrent bookings can each pass the check and both insert; only the
    /// per-seat unique index stops that, and it caps at the LAYOUT size, not at a
    /// smaller CapacityOverride (and not at all for open seating). After the
    /// insert commits we re-count; if this row pushed the session past its
    /// effective capacity we remove it and fail closed.</summary>
    private async Task EnforceCapacityAfterInsertAsync(
        SeatReservation reservation, int effectiveCap,
        CancellationToken cancellationToken)
    {
        var active = await appDbContext.SeatReservations
            .Where(r => r.SessionId == reservation.SessionId && r.ReleasedAt == null)
            .CountAsync(cancellationToken);
        if (active > effectiveCap)
        {
            appDbContext.SeatReservations.Remove(reservation);
            await appDbContext.SaveChangesAsync(cancellationToken);
            throw new ApiException(
                ErrorCodes.SeatSessionFull, 409,
                "No seats remain in this session.",
                "لا توجد مقاعد متبقية في هذه الجلسة.");
        }
    }

    /// <summary>M-2/M-1 (#21 — Round-1 held) — insert a Pending hold only while the
    /// session is below <paramref name="effectiveCap"/>, with the capacity COUNT and
    /// the INSERT in ONE SERIALIZABLE transaction so concurrent reserve-random /
    /// open-seating joins can neither oversell nor over-reject. The COUNT takes a
    /// key-range lock on (SessionId, ReleasedAt), so a concurrent insert cannot slip a
    /// phantom row in between the count and the save. Run through the EF execution
    /// strategy so it composes with <c>EnableRetryOnFailure</c> (a manual transaction
    /// under the retrying strategy throws otherwise): a serialization/deadlock victim
    /// re-runs the whole unit and the re-count then sees the committed rival, so the
    /// session fills to exactly the declared capacity — no oversell, no over-reject.
    /// <paramref name="build"/> runs INSIDE the transaction (so reserve-random's
    /// free-seat scan reads range-locked, consistent state) and returns the row to
    /// insert, or null when a specific seat is needed but none is free. Throws
    /// <see cref="ErrorCodes.SeatSessionFull"/> when full.</summary>
    private async Task<SeatReservation> InsertHoldWithinCapacityAsync(
        Guid sessionId, int effectiveCap,
        Func<CancellationToken, Task<SeatReservation?>> build,
        CancellationToken cancellationToken)
    {
        SeatReservation? added = null;
        SeatReservation? committed = null;
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A retry re-enters here; drop the row a failed attempt left tracked so the
            // next SaveChanges never re-inserts a stale (rolled-back) entity.
            if (added is not null)
            {
                appDbContext.Entry(added).State = EntityState.Detached;
                added = null;
            }
            committed = null;

            await using var tx = await appDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            var active = await appDbContext.SeatReservations
                .Where(r => r.SessionId == sessionId && r.ReleasedAt == null)
                .CountAsync(cancellationToken);
            if (active >= effectiveCap)
            {
                return; // full — the transaction rolls back on dispose
            }

            var reservation = await build(cancellationToken);
            if (reservation is null)
            {
                return; // capacity has room but no seat is free — treat as full
            }

            appDbContext.SeatReservations.Add(reservation);
            added = reservation;
            await appDbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            committed = reservation;
        });

        return committed ?? throw new ApiException(
            ErrorCodes.SeatSessionFull, 409,
            "No seats remain in this session.",
            "لا توجد مقاعد متبقية في هذه الجلسة.");
    }

    /// <summary>#21 — the session's currently-held seat-specific cells (row + number),
    /// read inside the serializable transaction so reserve-random picks a free seat
    /// against range-locked, consistent state. Open-seating rows (null row/seat) are
    /// excluded.</summary>
    private async Task<IReadOnlySet<(string Row, int Seat)>> LoadHeldSeatsAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var occupied = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null
                && r.RowLabel != null)
            .Select(r => new { r.RowLabel, r.SeatNumber })
            .ToListAsync(cancellationToken);
        return occupied
            .Select(o => (Row: o.RowLabel!, Seat: o.SeatNumber!.Value))
            .ToHashSet();
    }

    /// <summary>#21 — the first free seat (row-major over the layout) as a fresh
    /// confirmed RandomAssignment hold, or null when every seat is taken. Built with
    /// the captured <paramref name="now"/> so a transaction retry stamps the same
    /// created-at / expiry window.</summary>
    private static SeatReservation? PickRandomSeat(
        SessionContext ctx, IReadOnlySet<(string Row, int Seat)> taken,
        Guid actorUserId, DateTimeOffset now, bool callerIsVip)
    {
        // D-767 — index loop so each row's free-seat scan stops at ITS own count
        // (ctx.SeatCounts[i]); a ragged layout never yields a phantom seat on a short row.
        for (var i = 0; i < ctx.RowLabels.Count; i++)
        {
            // D-771 — skip whole rows the caller may not sit in (a VVIP protocol row,
            // or a VIP row for a non-VIP visitor), so an auto-pick can never hand out
            // a seat the self-pick would have refused.
            if (!IsSelfReservable(ctx.SeatTiers[i], callerIsVip))
            {
                continue;
            }
            var rowLabel = ctx.RowLabels[i];
            for (var seat = 1; seat <= ctx.SeatCounts[i]; seat++)
            {
                if (taken.Contains((rowLabel, seat)))
                {
                    continue;
                }
                return new SeatReservation
                {
                    Id = Guid.NewGuid(),
                    SessionId = ctx.SessionId,
                    RowLabel = rowLabel,
                    SeatNumber = seat,
                    Kind = SeatReservationKind.RandomAssignment,
                    ReservedForUserId = actorUserId,
                    CreatedByUserId = actorUserId,
                    CreatedAt = now,
                    // 2026-07-18 (reservation-only) — confirmed on create, no approval.
                    Status = BookingStatus.Approved,
                    // #6/#17 — the no-show release deadline: 3 minutes before start.
                    Expires = ctx.Start - NoShowReleaseGrace,
                };
            }
        }
        return null;
    }

    private Task<SeatReservation?> GetMyActiveAsync(
        Guid sessionId, Guid actorUserId, CancellationToken cancellationToken) =>
        appDbContext.SeatReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.SessionId == sessionId
                && r.ReservedForUserId == actorUserId
                && r.ReleasedAt == null, cancellationToken);

    private async Task PersistWithUniquenessGuardAsync(
        SeatReservation reservation, CancellationToken cancellationToken)
    {
        appDbContext.SeatReservations.Add(reservation);
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            appDbContext.Entry(reservation).State = EntityState.Detached;
            var message = ex.InnerException?.Message ?? ex.Message;
            if (message.Contains("ReservedForUserId", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApiException(
                    ErrorCodes.SeatAlreadyOwnedBySession, 409,
                    "You already have a seat reserved for this session.",
                    "لديك مقعد محجوز بالفعل لهذه الجلسة.");
            }
            throw new ApiException(
                ErrorCodes.SeatAlreadyReserved, 409,
                "That seat is already reserved.",
                "هذا المقعد محجوز بالفعل.");
        }
    }

    // M-4 / #6 — notify the attendee that their seat reservation was released:
    // either by an administrator (default) or by the pre-start no-show sweep
    // (noShow=true, so the message explains they were not checked in). Same
    // swallow-and-log discipline as the other booking notifications: a
    // notification failure never rolls back the release (the dispatcher writes to
    // the Identity DB, already committed here).
    private async Task TryNotifyBookingReleasedAsync(
        SeatReservation booking, (string Title, string TitleArabic) session,
        CancellationToken cancellationToken, bool noShow = false)
    {
        if (booking.ReservedForUserId is not { } userId)
        {
            return;
        }
        var seat = booking.RowLabel is { } row ? $"{row}{booking.SeatNumber}" : null;
        var (body, bodyArabic) = noShow
            ? (seat is not null
                    ? $"Your seat {seat} for \"{session.Title}\" was released because you did not check in before the session started."
                    : $"Your place in \"{session.Title}\" was released because you did not check in before the session started.",
               seat is not null
                    ? $"تم إلغاء مقعدك {seat} لجلسة \"{session.TitleArabic}\" لعدم تسجيل دخولك قبل بدء الجلسة."
                    : $"تم إلغاء حضورك لجلسة \"{session.TitleArabic}\" لعدم تسجيل دخولك قبل بدء الجلسة.")
            : (seat is not null
                    ? $"Your seat {seat} for \"{session.Title}\" was released by the organiser."
                    : $"Your place in \"{session.Title}\" was released by the organiser.",
               seat is not null
                    ? $"تم إلغاء مقعدك {seat} لجلسة \"{session.TitleArabic}\" من قبل المنظّم."
                    : $"تم إلغاء حضورك لجلسة \"{session.TitleArabic}\" من قبل المنظّم.");
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.BookingReleased,
                Title = "Seat reservation released",
                TitleArabic = "تم إلغاء حجز المقعد",
                Body = body,
                BodyArabic = bodyArabic,
                Severity = NotificationSeverity.Warning,
                RelatedEntityType = "Session",
                RelatedEntityId = booking.SessionId,
                SendEmail = false,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Booking-released notification failed for reservation {ReservationId}",
                booking.Id);
        }
    }

    private static MySeatReservation ToMine(SeatReservation r) =>
        new(r.Id, r.SessionId, r.RowLabel, r.SeatNumber, r.Kind, r.CreatedAt, r.Status);

    private sealed record SessionSnapshot(
        Guid Id, Guid HallId, int? CapacityOverride, string Title, string TitleArabic,
        DateTimeOffset Start, DateTimeOffset End,
        SeatSelectionMode? SeatSelectionModeOverride);
    private sealed record SessionContext(
        Guid SessionId, Guid HallId, int? CapacityOverride, int HallCapacity,
        HallSeatLayout Layout, IReadOnlyList<string> RowLabels,
        string SessionTitle, string SessionTitleArabic,
        DateTimeOffset Start, DateTimeOffset End,
        SeatSelectionMode EffectiveMode,
        // D-767 — the expanded per-row seat counts (one per RowLabels entry; a repeat of
        // SeatsPerRow when the layout is uniform). Every per-seat bound/capacity/random-
        // pick decision reads this array so uniform and variable layouts share one path.
        IReadOnlyList<int> SeatCounts,
        // D-771 — the expanded per-row seat TIERS (one per RowLabels entry; all Normal
        // for a legacy layout that stores none). Every eligibility decision reads this
        // array so the self-pick, the random pick and the seat map can never disagree.
        IReadOnlyList<SeatTier> SeatTiers);
}
