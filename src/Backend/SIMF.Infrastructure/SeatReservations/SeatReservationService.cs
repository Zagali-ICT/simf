// Tests: SIMF.Api.Tests/SeatReservationsTests.cs,
//        SIMF.Api.Tests/SeatTierEligibilityTests.cs,
//        SIMF.Api.Tests/ReservationNoShowReleaseWorkerTests.cs,
//        SIMF.Api.Tests/SeatChangeTests.cs,
//        SIMF.Api.Tests/GridContractTests.cs,
//        SIMF.Api.Tests/BookingsExcelTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Notifications;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Sessions;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SeatReservations;

/// <summary>Per-session seat
/// reservation orchestration. Hall stays frozen — layout columns
/// live on <c>HallSeatLayout</c>. Active uniqueness is enforced by
/// filtered unique indexes; release sets <c>ReleasedAt</c> and frees
/// the slot for re-booking. Per-session capacity = layout total
/// (rows*seats), further capped by the smaller of
/// <c>Session.CapacityOverride</c> and <c>Hall.Capacity</c>.</summary>
internal sealed class SeatReservationService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    INotificationDispatcher notifications,
    TimeProvider timeProvider,
    IQrResolver qrResolver,
    ILogger<SeatReservationService> logger) : ISeatReservationService
{
    /// <summary>How long before a session starts an
    /// un-checked-in seat reservation is auto-released so the seat can go to
    /// someone else ("cancelled if you don't check in 3 minutes before start").
    /// A reservation's <c>NoShowReleaseAt</c> is stamped at <c>Start - NoShowReleaseGrace</c>;
    /// the no-show release scan reads it. Defined here (not on the worker) so the
    /// stamp written at creation and the scan that reads it share one source.</summary>
    internal static readonly TimeSpan NoShowReleaseGrace = TimeSpan.FromMinutes(3);

    /// <summary>The attendee profile a seat is held against, for a caller known
    /// only as a signed-in account. A seat belongs to an ATTENDEE, so an account
    /// carrying no profile — an admin-typed user — cannot hold one and is refused
    /// here rather than booking a seat that resolves to nobody.
    ///
    /// <para>The ACTOR columns (<c>CreatedByUserId</c>, <c>ReleasedByUserId</c>)
    /// and the audit trail keep the account id: who did it and who it is for are
    /// different questions, and on an admin block they are different people.</para>
    ///
    /// <para>Approval CREATES the attendee record when none exists
    /// (<c>AdminAccountService.EnsureUserProfileAsync</c>), so an approved account
    /// always has one and this refusal should be unreachable from the app. It
    /// firing means an account reached an approved state without going through
    /// approval.</para></summary>
    private async Task<Guid> ActorProfileIdAsync(
        Guid actorUserId, CancellationToken cancellationToken) =>
        await appDbContext.ProfileIdForAccountAsync(actorUserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AttendeeProfileMissing, 403,
                "This account has no attendee record, so it cannot hold a seat.",
                "لا يوجد سجل حاضر مرتبط بهذا الحساب، لذلك لا يمكنه حجز مقعد.");

    /// <summary>The Identity account behind an attendee profile, or null when they
    /// hold none — the ordinary case for a walk-in or a bulk-minted badge. It is
    /// how the notification paths skip an attendee they have no way to reach.</summary>
    private Task<Guid?> AttendeeAccountIdAsync(
        Guid attendeeProfileId, CancellationToken cancellationToken) =>
        appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == attendeeProfileId)
            .Select(profile => profile.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>The same question as <see cref="AttendeeAccountIdAsync"/> for a whole
    /// SET of attendees, in one query — the no-show sweep frees many holds at once and
    /// asked it a row at a time. A profile that holds no account is simply absent from
    /// the result, which is how a caller skips an attendee it cannot reach.</summary>
    private async Task<Dictionary<Guid, Guid>> AttendeeAccountIdsAsync(
        IReadOnlyList<Guid> attendeeProfileIds, CancellationToken cancellationToken) =>
        (await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => attendeeProfileIds.Contains(profile.Id)
                && profile.UserId != null)
            .Select(profile => new { profile.Id, AccountId = profile.UserId!.Value })
            .ToListAsync(cancellationToken))
        .ToDictionary(profile => profile.Id, profile => profile.AccountId);

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
                r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.ReservedForProfileId,
                // Carry the booking status so MyCell can drive the app's
                // seat-card hint (Pending → await approval / Approved → show badge).
                r.Status,
                // The admin-typed VVIP guest hint travels with the cell so a
                // protocol seat shows "reserved for the Minister" instead of a bare
                // blocked square.
                r.GuestHint, r.GuestHintArabic,
            })
            .ToListAsync(cancellationToken);

        // The "confirmed" (تم التأكيد) seat state: a reservation whose
        // holder has an OPEN HallAttendance row for this session (scanned in at the
        // hall gate). One query for the whole session, matched by holder id.
        var checkedInProfileIds = (await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.Leave == null)
            .Select(a => a.UserProfileId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var cells = reservations.Select(r => new SessionSeatCell(
            r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
            r.ReservedForProfileId is { } holder && checkedInProfileIds.Contains(holder),
            r.GuestHint, r.GuestHintArabic))
            .ToList();

        // The caller signs in as an ACCOUNT, but their seat is held against their
        // attendee profile, so resolve that before looking for "my" cell.
        SessionSeatCell? mine = null;
        var actorProfileId = actorUserId is { } mapActor
            ? await appDbContext.ProfileIdForAccountAsync(mapActor, cancellationToken)
            : null;
        if (actorProfileId is { } actor)
        {
            var ownRow = reservations.FirstOrDefault(r => r.ReservedForProfileId == actor);
            if (ownRow is not null)
            {
                mine = new SessionSeatCell(
                    ownRow.Id, ownRow.RowLabel, ownRow.SeatNumber, ownRow.Kind,
                    ownRow.Status,
                    ownRow.ReservedForProfileId is { } ownHolder
                        && checkedInProfileIds.Contains(ownHolder),
                    ownRow.GuestHint, ownRow.GuestHintArabic);
            }
        }

        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => new { h.Capacity, h.SeatSelectionMode })
            .SingleAsync(cancellationToken);
        // A hall/session with no seat layout has no assignable seats, so it
        // is inherently open seating (a one-tap join); otherwise honour the session
        // override, else the hall's configured mode. Without this, a seeded session
        // (AssignedSeat default, no layout) opened an empty seat picker — the "join
        // not working" the owner reported.
        var hasLayout = rowLabels.Count > 0 && (layout?.SeatsPerRow ?? 0) > 0;
        var effectiveMode = EffectiveMode(
            session.SeatSelectionModeOverride, hall.SeatSelectionMode, hasLayout);

        // Emit the per-row counts only for a ragged layout; null (key omitted)
        // for a uniform one keeps the shipped wire identical for old and new apps, which
        // then render every row at the still-emitted seatsPerRow (= max for a ragged layout).
        var seatCounts = string.IsNullOrWhiteSpace(layout?.SeatCounts)
            ? null
            : ExpandSeatCounts(layout, rowLabels);

        // Always emit ONE tier per row (Normal for a layout saved before tiers) so the
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
            // The session title is already loaded in the snapshot.
            session.Title, session.TitleArabic,
            // The effective mode drives the app's Join CTA.
            effectiveMode,
            // The ragged per-row counts (null = uniform).
            seatCounts,
            // The per-row tiers + the caller's own VIP tier.
            seatTiers, callerIsVip);
    }

    /// <summary>The mode the app branches its Join CTA on. A session with
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
        // The seat TIER gate, enforced on the SERVER (the app only greys the
        // ineligible seats out): a VVIP seat is never self-reservable, a VIP seat
        // needs the VIP tier, a Normal seat is open to everyone.
        EnsureTierEligible(
            ctx.SeatTiers[RowIndex(ctx.RowLabels, row)],
            await IsVipVisitorAsync(actorUserId, cancellationToken));
        await EnsureSessionHasCapacityAsync(ctx, cancellationToken);

        var actorProfileId = await ActorProfileIdAsync(actorUserId, cancellationToken);
        await EnsureNoActiveHoldAsync(
            sessionId, actorProfileId, openSeating: false, cancellationToken);
        await EnsureNoOverlapAsync(
            sessionId, actorProfileId, ctx.Start, ctx.End, cancellationToken);
        await EnsureSeatIsFreeAsync(sessionId, row, seat, cancellationToken);

        // The capacity COUNT, the three booking guards and the INSERT run in ONE
        // serializable transaction, the same shape the random and open-seating paths
        // use. It replaces an insert-then-compensate backstop that counted AFTER its
        // own commit: two visitors picking two DIFFERENT free seats for the last
        // place both committed, both then counted one over the cap, both removed
        // their own row and both were told the session was full — leaving the place
        // empty and two visitors refused. It also made the two read-then-write guards
        // above advisory: the same-attendee and overlapping-session checks ran
        // outside any transaction, so two devices booking two overlapping sessions
        // at once both passed and BOOKING_OVERLAP never fired for the very case it
        // exists to stop. Re-checked inside the transaction, one of them waits for
        // the other and then sees it.
        var now = timeProvider.SimfNow();
        var reservation = await InsertHoldWithinCapacityAsync(
            sessionId, EffectiveCapacity(ctx),
            async ct =>
            {
                await EnsureNoActiveHoldAsync(
                    sessionId, actorProfileId, openSeating: false, ct);
                await EnsureNoOverlapAsync(
                    sessionId, actorProfileId, ctx.Start, ctx.End, ct);
                await EnsureSeatIsFreeAsync(sessionId, row, seat, ct);
                return new SeatReservation
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    RowLabel = row,
                    SeatNumber = seat,
                    Kind = SeatReservationKind.UserBooking,
                    ReservedForProfileId = actorProfileId,
                    CreatedByUserId = actorUserId,
                    // Captured outside the strategy so a retry stamps the same
                    // created-at, exactly as the random pick does.
                    CreatedAt = now,
                    // 2026-07-18 (reservation-only) — there is no Control Panel
                    // approval step: the reservation is confirmed the moment it is
                    // made. It stays a provisional hold until the visitor checks in
                    // at the hall gate; the pre-start sweep releases any hold that
                    // never checks in.
                    Status = BookingStatus.Approved,
                    // The no-show release deadline: 3 minutes before the session
                    // starts. If the holder has not checked in by then the seat is
                    // freed.
                    NoShowReleaseAt = ctx.Start - NoShowReleaseGrace,
                };
            },
            cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SeatReservationCreated,
            actorUserId,
            $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={row}; seat={seat}; kind=UserBooking; status=Approved",
            cancellationToken);

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

        var actorProfileId = await ActorProfileIdAsync(actorUserId, cancellationToken);
        await EnsureNoActiveHoldAsync(
            sessionId, actorProfileId, openSeating: false, cancellationToken);
        await EnsureNoOverlapAsync(
            sessionId, actorProfileId, ctx.Start, ctx.End, cancellationToken);

        // The capacity COUNT, the free-seat pick and the INSERT run in
        // ONE Serializable transaction so concurrent reserve-random can neither
        // oversell (the key-range lock serialises count-then-insert) nor over-reject
        // (a deadlock victim re-runs and its re-count sees the committed rival),
        // filling exactly the declared capacity. See InsertHoldWithinCapacityAsync.
        var now = timeProvider.SimfNow();
        // The auto-pick must respect the same tier rule as the self-pick, so
        // resolve the caller's VIP tier ONCE (outside the serializable transaction —
        // it is a read of admin-curated profile data, not of the seat state).
        var callerIsVip = await IsVipVisitorAsync(actorUserId, cancellationToken);
        var reservation = await InsertHoldWithinCapacityAsync(
            sessionId, EffectiveCapacity(ctx),
            async ct =>
            {
                // Re-checked INSIDE the serializable transaction: outside one they
                // are advisory, and a second request that raced this one past them
                // reached the insert and died on the (SessionId,
                // ReservedForProfileId) unique index as a raw 500.
                await EnsureNoActiveHoldAsync(
                    sessionId, actorProfileId, openSeating: false, ct);
                await EnsureNoOverlapAsync(
                    sessionId, actorProfileId, ctx.Start, ctx.End, ct);
                var taken = await LoadHeldSeatsAsync(sessionId, ct);
                return PickRandomSeat(
                    ctx, taken, actorProfileId, actorUserId, now, callerIsVip);
            },
            cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SeatReservationCreated,
            actorUserId,
            $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={reservation.RowLabel}; seat={reservation.SeatNumber}; "
                + "kind=RandomAssignment; status=Approved",
            cancellationToken);

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
        // Resolve the effective mode with the no-layout rule so a session
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

        var actorProfileId = await ActorProfileIdAsync(actorUserId, cancellationToken);
        await EnsureNoActiveHoldAsync(
            sessionId, actorProfileId, openSeating: true, cancellationToken);
        await EnsureNoOverlapAsync(
            sessionId, actorProfileId, session.Start, session.End, cancellationToken);

        // Open-seating capacity = the session override, else the hall
        // capacity (no seat layout bounds it), and there is NO per-seat DB backstop.
        // So the capacity COUNT and the INSERT run in ONE Serializable transaction
        // (via the execution strategy so it composes with EnableRetryOnFailure):
        // concurrent joins can neither oversell — the key-range lock serialises
        // count-then-insert — nor over-reject. See InsertHoldWithinCapacityAsync.
        var declaredCap = session.CapacityOverride ?? hall.Capacity;
        var now = timeProvider.SimfNow();
        var reservation = await InsertHoldWithinCapacityAsync(
            sessionId, declaredCap,
            async ct =>
            {
                // Re-checked INSIDE the serializable transaction, for the same
                // reason the seat paths do it: outside one they are advisory.
                await EnsureNoActiveHoldAsync(
                    sessionId, actorProfileId, openSeating: true, ct);
                await EnsureNoOverlapAsync(
                    sessionId, actorProfileId, session.Start, session.End, ct);
                return new SeatReservation
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    RowLabel = null,
                    SeatNumber = null,
                    Kind = SeatReservationKind.OpenSeating,
                    ReservedForProfileId = actorProfileId,
                    CreatedByUserId = actorUserId,
                    CreatedAt = now,
                    // 2026-07-18 (reservation-only) — confirmed on create, no
                    // approval step; the hold stays provisional until hall check-in.
                    Status = BookingStatus.Approved,
                    // The no-show release deadline: 3 minutes before start.
                    NoShowReleaseAt = session.Start - NoShowReleaseGrace,
                };
            },
            cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SeatReservationCreated,
            actorUserId,
            $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + "kind=OpenSeating; status=Approved",
            cancellationToken);

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
        // Deliberate owner rule: a self-service change of seat is allowed only
        // BEFORE the session starts, the SAME boundary the cancel already enforces.
        // Once the session is running the seat plan is what the
        // staff seating desk and the gate flow work from on the floor, and the
        // pre-start no-show sweep has already redistributed the un-checked-in holds;
        // a visitor reshuffling themselves at that point would desync the desk. A
        // move during a live session goes through staff, not the app.
        EnsureSessionNotStarted(ctx.Start);
        ValidateSeatBounds(ctx, row, seat);
        // The DESTINATION seat must pass exactly the same tier gate as a
        // first reservation, via the one shared rule: a VVIP seat is never
        // self-reservable, a VIP seat needs the VIP tier.
        EnsureTierEligible(
            ctx.SeatTiers[RowIndex(ctx.RowLabels, row)],
            await IsVipVisitorAsync(actorUserId, cancellationToken));

        var actorProfileId = await ActorProfileIdAsync(actorUserId, cancellationToken);
        var moved = await MoveHoldAtomicallyAsync(
            ctx, actorProfileId, actorUserId, row, seat, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SeatReservationMoved,
            actorUserId,
            $"reservationId={moved.Reservation.Id}; sessionId={sessionId}; "
                + $"fromRow={moved.FromRowLabel}; fromSeat={moved.FromSeatNumber}; "
                + $"row={row}; seat={seat}; kind=UserBooking; status=Approved",
            cancellationToken);

        logger.LogInformation(
            "Seat moved from {FromRow}{FromSeat} to {Row}{Seat} on session {SessionId} by user {Actor}",
            moved.FromRowLabel, moved.FromSeatNumber, row, seat, sessionId, actorUserId);

        return ToMine(moved.Reservation);
    }

    /// <summary>The ATOMIC half of the seat change: release the held seat and
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
    /// (SessionId, ReservedForProfileId) — would otherwise reject the new row while
    /// the old one is still held; statement order inside a single SaveChanges batch
    /// is an EF implementation detail, so it is made explicit here.</para></summary>
    private async Task<(SeatReservation Reservation, string? FromRowLabel, int? FromSeatNumber)>
        MoveHoldAtomicallyAsync(
            SessionContext ctx, Guid actorProfileId, Guid actorUserId,
            string row, int seat,
            CancellationToken cancellationToken)
    {
        SeatReservation? origin = null;
        SeatReservation? added = null;
        string? addedFromRowLabel = null;
        int? addedFromSeatNumber = null;
        (SeatReservation Reservation, string? FromRowLabel, int? FromSeatNumber)? committed = null;
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A retry re-enters here; drop everything a rolled-back attempt left
            // tracked so the next SaveChanges neither re-inserts a stale row nor
            // re-applies a release the database has already thrown away.
            //
            // A retry can also mean the commit acknowledgement was lost rather than
            // the commit: the move is already stored, and re-running would re-read the
            // origin, find the destination row this very attempt created and answer
            // SEAT_MOVE_SAME_SEAT for a move that succeeded. The destination id is
            // client-generated, so finding it means the attempt committed.
            committed = null;
            var previous = added;
            var recovered = false;
            if (previous is not null)
            {
                var previousId = previous.Id;
                recovered = await appDbContext.SeatReservations.AsNoTracking()
                    .AnyAsync(r => r.Id == previousId, cancellationToken);
                appDbContext.Entry(previous).State = EntityState.Detached;
                added = null;
            }
            if (origin is not null)
            {
                appDbContext.Entry(origin).State = EntityState.Detached;
                origin = null;
            }
            if (recovered)
            {
                committed = (previous!, addedFromRowLabel, addedFromSeatNumber);
                return;
            }

            await using var tx = await appDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            origin = await appDbContext.SeatReservations
                .SingleOrDefaultAsync(r => r.SessionId == ctx.SessionId
                    && r.ReservedForProfileId == actorProfileId
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

            var now = timeProvider.SimfNow();
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
                ReservedForProfileId = actorProfileId,
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                Status = BookingStatus.Approved,
                // The moved hold keeps the SAME no-show deadline as the seat
                // it replaces: 3 minutes before the session starts.
                NoShowReleaseAt = ctx.Start - NoShowReleaseGrace,
            };
            appDbContext.SeatReservations.Add(target);
            added = target;
            // Carried outside the attempt so a retry that discovers this row already
            // committed can still report which seat the holder came FROM.
            addedFromRowLabel = fromRow;
            addedFromSeatNumber = fromSeat;
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
        var actorProfileId = await ActorProfileIdAsync(actorUserId, cancellationToken);
        var mine = await appDbContext.SeatReservations
            .Where(r => r.SessionId == sessionId
                && r.ReservedForProfileId == actorProfileId
                && r.ReleasedAt == null)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SeatReservationNotFound, 404,
                "You do not have a seat to release in this session.",
                "ليس لديك مقعد للإلغاء في هذه الجلسة.");

        // A booking can only be cancelled BEFORE the session starts.
        var sessionStart = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => (DateTime?)s.Start)
            .SingleOrDefaultAsync(cancellationToken);
        if (sessionStart is { } start && timeProvider.SimfNow() >= start)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionStarted, 409,
                "You cannot cancel a booking after the session has started.",
                "لا يمكنك إلغاء الحجز بعد بدء الجلسة.");
        }

        var now = timeProvider.SimfNow();
        mine.ReleasedAt = now;
        mine.Status = BookingStatus.Cancelled;
        await appDbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteSuccessAsync(
            AuditEvents.BookingCancelled,
            actorUserId,
            $"reservationId={mine.Id}; sessionId={sessionId}; "
                + $"row={mine.RowLabel}; seat={mine.SeatNumber}; kind={mine.Kind}",
            cancellationToken);
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
        // The expanded per-row counts drive the capacity (sum), matching
        // rows × seatsPerRow when uniform; the wire field stays null when uniform so the
        // CP editor reads back exactly what it wrote.
        var expanded = layout is null
            ? Array.Empty<int>()
            : ExpandSeatCounts(layout, rowLabels);
        var seatCounts = string.IsNullOrWhiteSpace(layout?.SeatCounts) ? null : expanded;
        // The editor always reads back one tier per row (Normal for a legacy
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
        // Resolve the per-row seat counts. When SeatCounts is supplied it is
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
            if (requestedCounts.Any(count => count is < 1 or > 80))
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

        // Resolve the per-row seat TIERS (owner 2026-07-26). Supplied →
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
            if (requestedTiers.Any(tier => !Enum.IsDefined(typeof(SeatTier), tier)))
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
            // Keep the stored tiers, re-aligned to the new row set by row LABEL —
            // the same OrdinalIgnoreCase match the orphan guard beside this uses.
            // Re-aligning by POSITION shifted every tier the moment a row was
            // inserted ahead of an existing one: stored ["A","B"] with tiers
            // [Normal,Vvip], re-saved as ["A0","A","B"] with the tiers omitted, gave
            // row A the tier that belonged to row B, so a row that was bookable
            // yesterday started refusing every visitor with SEAT_TIER_RESERVED and
            // nothing in the response said the tiers had moved. A label the layout
            // did not previously carry still inherits the owner's VVIP default.
            var storedRows = ParseRowLabels(layout.RowLabels);
            var stored = ExpandSeatTiers(layout, storedRows);
            seatTiers = new List<SeatTier>(rows.Count);
            foreach (var label in rows)
            {
                var storedIndex = RowIndex(storedRows, label);
                seatTiers.Add(storedIndex >= 0 ? stored[storedIndex] : SeatTier.Vvip);
            }
        }
        var tiersCsv = string.Join(',', seatTiers.Select(tier => (int)tier));

        // An existing layout may already back active reservations; a change
        // that drops a row or shrinks a row's seat count would strand any seat that
        // now falls outside the grid. Block it (the operator must release those
        // seats first). A first-time layout (layout is null) can have no seat-
        // specific reservations yet — the reserve paths require a layout.
        if (layout is not null)
        {
            await EnsureLayoutChangeKeepsActiveReservationsAsync(
                hallId, rows, seatCounts, cancellationToken);
        }
        var now = timeProvider.SimfNow();
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallSeatLayoutUpdated,
            actorUserId,
            $"hallId={hallId}; rows={rowsCsv}; seatsPerRow={seatsPerRow}; "
                + $"seatCounts={countsCsv ?? "(uniform)"}; seatTiers={tiersCsv}",
            cancellationToken);

        return new HallSeatLayoutSnapshot(
            hallId, rows, seatsPerRow, layoutCapacity, hall.Capacity,
            variable ? seatCounts : null, seatTiers);
    }

    public async Task<HallSeatLayoutSnapshot> DeleteLayoutAsync(
        Guid actorUserId, Guid hallId,
        CancellationToken cancellationToken = default)
    {
        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == hallId)
            .Select(h => new { h.Id, h.Capacity })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.HallNotFound, 404,
                "Hall not found.",
                "لم يتم العثور على القاعة.");

        var layout = await appDbContext.HallSeatLayouts
            .SingleOrDefaultAsync(l => l.HallId == hallId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SeatLayoutMissing, 404,
                "This hall does not have a seat layout to remove.",
                "لا يوجد مخطط مقاعد لهذه القاعة لإزالته.");

        // The same orphan rule SetLayoutAsync enforces through
        // EnsureLayoutChangeKeepsActiveReservationsAsync, applied to the
        // hardest possible shrink: removing the grid strands EVERY active
        // seat-specific reservation, so any single one blocks the delete and the
        // error names how many the operator must release first.
        var blocking = await CountActiveSeatReservationsAsync(hallId, cancellationToken);
        if (blocking > 0)
        {
            throw new ApiException(
                ErrorCodes.SeatLayoutHasReservations, 409,
                $"Removing this layout would strand {blocking} active seat reservation(s). "
                + "Release them before removing the layout.",
                $"ستؤدي إزالة هذا المخطط إلى إلغاء {blocking} حجز مقعد نشط. "
                + "يرجى إلغاء هذه الحجوزات قبل إزالة المخطط.");
        }

        appDbContext.HallSeatLayouts.Remove(layout);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallSeatLayoutDeleted,
            actorUserId,
            $"hallId={hallId}; rows={layout.RowLabels}; "
                + $"seatsPerRow={layout.SeatsPerRow}; "
                + $"seatCounts={layout.SeatCounts ?? "(uniform)"}",
            cancellationToken);

        logger.LogInformation(
            "Seat layout removed for hall {HallId} by user {Actor} — the hall reverts "
            + "to general admission.", hallId, actorUserId);

        // The hall is now general admission: no rows, no seats, zero layout capacity.
        return new HallSeatLayoutSnapshot(
            hallId, Array.Empty<string>(), 0, 0, hall.Capacity,
            SeatCounts: null, SeatTiers: Array.Empty<SeatTier>());
    }

    public async Task AdminReserveRowAsync(
        Guid actorUserId, Guid sessionId,
        AdminReserveRowRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        // Resolve the row's index so the block fills exactly THIS row's seat
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
            occupiedInRow
                .Where(seatNumber => seatNumber.HasValue)
                .Select(seatNumber => seatNumber!.Value));

        var free = new List<int>();
        for (var seat = 1; seat <= ctx.SeatCounts[rowIndex]; seat++)
        {
            if (!taken.Contains(seat))
            {
                free.Add(seat);
            }
        }

        var inserted = await BlockRowSeatsAsync(
            sessionId, row, free, actorUserId, timeProvider.SimfNow(), cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SeatRowAdminReserved,
            actorUserId,
            $"sessionId={sessionId}; row={row}; inserted={inserted}",
            cancellationToken);
    }

    /// <summary>Write an administrator's row block. Every still-free seat goes in
    /// ONE <c>SaveChanges</c>, whose implicit transaction leaves the row either
    /// wholly blocked or wholly untouched. The seat-at-a-time save this replaces was
    /// one round trip per seat with nothing enclosing it, so a cancelled request or a
    /// dropped connection part-way through an 80-seat row committed the seats it had
    /// reached, skipped the rest, never reached the audit line and answered the
    /// operator with a 500 — leaving the organiser a row that is half protocol
    /// seating and half still bookable, with nothing to say which half.
    ///
    /// <para>A concurrent self-pick that takes one of these seats first fails the
    /// whole batch on the per-seat filtered unique index. That case falls back to the
    /// seat-at-a-time walk, which skips whatever was taken and blocks the rest —
    /// exactly the old behaviour, now only on the raced path. Returns how many seats
    /// were actually blocked.</para></summary>
    private async Task<int> BlockRowSeatsAsync(
        Guid sessionId, string row, IReadOnlyList<int> seats,
        Guid actorUserId, DateTime now, CancellationToken cancellationToken)
    {
        if (seats.Count == 0)
        {
            return 0;
        }

        var batch = seats
            .Select(seat => NewRowBlock(sessionId, row, seat, actorUserId, now))
            .ToList();
        appDbContext.SeatReservations.AddRange(batch);
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
            return batch.Count;
        }
        catch (DbUpdateException ex)
        {
            foreach (var blocked in batch)
            {
                appDbContext.Entry(blocked).State = EntityState.Detached;
            }
            logger.LogWarning(
                ex,
                "Row block on {Row} of session {SessionId} raced a concurrent booking; "
                + "blocking the remaining seats one at a time.", row, sessionId);
        }

        var inserted = 0;
        foreach (var seat in seats)
        {
            try
            {
                await PersistWithUniquenessGuardAsync(
                    NewRowBlock(sessionId, row, seat, actorUserId, now),
                    cancellationToken);
                inserted++;
            }
            catch (ApiException ex) when (ex.Code == ErrorCodes.SeatAlreadyReserved)
            {
                // Lost a race against a concurrent self-pick — fine.
            }
        }
        return inserted;
    }

    /// <summary>One seat of an administrator's row block: no attendee, and
    /// confirmed immediately because a block is not a visitor booking and never
    /// enters the (dormant) approval queue.</summary>
    private static SeatReservation NewRowBlock(
        Guid sessionId, string row, int seat, Guid actorUserId, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = SeatReservationKind.AdminReservedRow,
            ReservedForProfileId = null,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            Status = BookingStatus.Approved,
        };

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

        await EnsureSeatIsFreeAsync(sessionId, row, seat, cancellationToken);

        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = SeatReservationKind.AdminReservedRow,
            ReservedForProfileId = null,
            CreatedByUserId = actorUserId,
            CreatedAt = timeProvider.SimfNow(),
            // An admin block is confirmed immediately (never enters the queue).
            Status = BookingStatus.Approved,
            // The manual guest hint. A VVIP seat has no registration, so this
            // free text IS the occupant record the app + the staff seating desk read.
            GuestHint = NormaliseHint(request.GuestHint),
            GuestHintArabic = NormaliseHint(request.GuestHintArabic),
        };
        await PersistWithUniquenessGuardAsync(reservation, cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SeatRowAdminReserved,
            actorUserId,
            $"sessionId={sessionId}; row={row}; seat={seat}; single=true; "
                + "guestHint="
                + ((reservation.GuestHint ?? reservation.GuestHintArabic) is null
                    ? "(none)" : "(set)"),
            cancellationToken);
    }

    /// <summary>Trim the admin-typed guest hint to null-or-content and reject
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
        // A release must also close the booking's lifecycle. Leaving Status
        // untouched left an Approved row with ReleasedAt set (a stale
        // "confirmed-but-gone" state the CP/app could still read as active), so mark
        // it Cancelled. ReleasedByUserId records the admin who performed THIS
        // RELEASE; the no-show sweep and the holder's own cancel leave it null.
        var now = timeProvider.SimfNow();
        reservation.ReleasedAt = now;
        reservation.Status = BookingStatus.Cancelled;
        reservation.ReleasedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        var eventType = reservation.Kind == SeatReservationKind.AdminReservedRow
            ? AuditEvents.SeatRowAdminReleased
            : AuditEvents.SeatReservationReleased;
        await auditLog.WriteSuccessAsync(
            eventType,
            actorUserId,
            $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={reservation.RowLabel}; seat={reservation.SeatNumber}; "
                + $"kind={reservation.Kind}",
            cancellationToken);

        // Tell the attendee an admin released their held/confirmed seat
        // (no-op for an AdminReservedRow block: ReservedForProfileId is null).
        var session = await LoadSessionTitleAsync(reservation.SessionId, cancellationToken);
        await TryNotifyBookingReleasedAsync(reservation, session, cancellationToken);
    }

    public async Task<GridPage<SeatPlanCell>> ListSessionReservationsAsync(
        Guid sessionId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

        var baseQuery = appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null);
        var total = await baseQuery.CountAsync(cancellationToken);
        // A11 — project the REAL Status (it used to fall through to the record
        // default, putting Pending on the wire for rows that are all Approved).
        var rows = await baseQuery
            .OrderBy(r => r.RowLabel).ThenBy(r => r.SeatNumber)
            .Skip(skip).Take(top)
            .Select(r => new
            {
                r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
                r.ReservedForProfileId, r.GuestHint, r.GuestHintArabic,
            })
            .ToListAsync(cancellationToken);

        // A11 — the "confirmed" seat state: the holder has an OPEN HallAttendance
        // row for this session. Same definition as the app/CP seat map, one query
        // for the whole page rather than per row.
        var checkedInProfileIds = (await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.Leave == null)
            .Select(a => a.UserProfileId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // DEF-SEA-001 — an admin must see WHOSE seat they are about to release, so
        // resolve the holders' bilingual names in one batch. Matched by PROFILE id,
        // which every holder has, so a walk-in's seat now names its occupant instead
        // of showing the admin a blank name above a Release button.
        var holderIds = rows
            .Where(r => r.ReservedForProfileId is not null)
            .Select(r => r.ReservedForProfileId!.Value)
            .Distinct()
            .ToList();
        var holders = holderIds.Count == 0
            ? new Dictionary<Guid, (string Name, string NameArabic)>()
            : (await appDbContext.UserProfiles.AsNoTracking()
                .Where(p => holderIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.NameArabic })
                .ToListAsync(cancellationToken))
                .ToDictionary(p => p.Id, p => (p.Name, p.NameArabic));

        var cells = rows.Select(r =>
        {
            var holder = r.ReservedForProfileId is { } id
                && holders.TryGetValue(id, out var found)
                ? found
                : (Name: string.Empty, NameArabic: string.Empty);
            return new SeatPlanCell(
                r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
                r.ReservedForProfileId is { } holderId
                    && checkedInProfileIds.Contains(holderId),
                r.ReservedForProfileId, holder.Name, holder.NameArabic,
                r.GuestHint, r.GuestHintArabic);
        }).ToList();

        return GridPage<SeatPlanCell>.Of(cells, total, skip, top);
    }

    // -- Booking monitor + no-show release --

    /// <summary>
    /// The grid contract for /admin/bookings: one entry per key BookingsList.razor
    /// can send, as both its filter and its sort. A key not declared here is a 400,
    /// not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<SeatReservation> ActiveBookingColumns =
        new GridColumns<SeatReservation>()
            // One page column carries both languages, and one key has to serve both
            // the sort and a filter that still matches an Arabic title. Add and
            // AddFilter cannot share a key, so the selector concatenates instead:
            // the contains then matches EITHER language, exactly as the OR it
            // replaces, and the sort is still by English title, that being the
            // prefix. Dropping the Arabic half would have left an Arabic operator a
            // filter box that silently returns the unfiltered set.
            .Add("session", reservation =>
                reservation.Session!.Title + " " + reservation.Session!.TitleArabic)
            .Add("start", reservation => reservation.Session!.Start)
            // Row label only. The hand-written switch this replaces also broke ties
            // on SeatNumber; a REQUESTED sort is one level plus the tiebreak here,
            // so seats within one row now fall to the id rather than to seat order.
            .Add("seat", reservation => reservation.RowLabel)
            // The page renders this column but marks it neither sortable nor
            // filterable, so nothing sends it today. Declared anyway, because the
            // name is a plain (unencrypted) profile column reached by an App-DB
            // navigation: the key works the day the page marks it, instead of 400ing.
            .Add("attendee", reservation => reservation.ReservedForProfile!.Name)
            .Add("bookedAt", reservation => reservation.CreatedAt)
            .DefaultOrder("bookedAt", descending: true)
            .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<SeatReservation, ActiveBookingRow>> ToActiveBooking =
        reservation => new ActiveBookingRow(
            reservation.Id,
            reservation.SessionId,
            reservation.Session!.Title,
            reservation.Session!.TitleArabic,
            reservation.Session!.Start,
            reservation.RowLabel,
            reservation.SeatNumber,
            reservation.Kind,
            reservation.ReservedForProfileId,
            // The scope predicate already excludes a null holder, but the guard
            // keeps the empty string — not a null — on the wire and in the Excel
            // export, which is what the second round-trip this replaced produced.
            reservation.ReservedForProfile == null
                ? string.Empty
                : reservation.ReservedForProfile.Name,
            reservation.CreatedAt);

    /// <summary>The read-only Control Panel monitor of ACTIVE (confirmed,
    /// still-held) visitor reservations across all sessions. There is no approval
    /// step — bookings auto-confirm — so this is a monitor, not a queue. Admin
    /// row-blocks are created Approved with a null <c>ReservedForProfileId</c>, so
    /// they never appear here.
    ///
    /// <para>The session title and the attendee name are both reached by App-DB
    /// navigations inside the projection, so every column is filtered, sorted and
    /// paged on the server — the attendee name used to come from a second
    /// round-trip after paging, which is why that column could not be sorted.</para>
    /// </summary>
    public Task<GridPage<ActiveBookingRow>> ListActiveBookingsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.SeatReservations
            .Where(reservation => reservation.Status == BookingStatus.Approved
                && reservation.ReleasedAt == null
                && reservation.ReservedForProfileId != null)
            .ToGridPageAsync(
                query, ActiveBookingColumns, reservation => reservation.Id,
                ToActiveBooking, cancellationToken);

    /// <summary>The no-show release: free
    /// every ACTIVE (Approved, still-held) visitor seat reservation whose no-show
    /// deadline (<c>NoShowReleaseAt</c> = the session's <c>Start − 3min</c>) has passed
    /// <b>and whose holder never checked in</b> (no <c>HallAttendance</c> for that
    /// session — arrival by gate scan or geofence). A walk-in who booked at or after
    /// the deadline (<c>CreatedAt &gt;= NoShowReleaseAt</c>) is exempt — they are present,
    /// not a no-show. Each freed holder is notified (<see cref="NotificationKind.BookingReleased"/>).
    /// Admin blocks (null holder) are never touched. Returns the number released.
    /// Called once per minute by <c>ReservationNoShowReleaseWorker</c>; extracted so
    /// the release rule is unit-tested at the service (not by driving the loop).</summary>
    public async Task<int> ReleaseNoShowsAsync(
        DateTime now, CancellationToken cancellationToken = default)
    {
        var released = await ReleaseDueNoShowsAsync(now, cancellationToken);
        if (released.Count == 0)
        {
            return 0;
        }

        // Audit + notify each freed no-show. The session titles and the holders'
        // Identity accounts are each resolved ONCE for the whole released set: the
        // account lookup used to be a query per row inside the dispatch.
        var sessionIds = released.Select(reservation => reservation.SessionId)
            .Distinct()
            .ToList();
        var titles = await appDbContext.Sessions.AsNoTracking()
            .Where(session => sessionIds.Contains(session.Id))
            .Select(session => new { session.Id, session.Title, session.TitleArabic })
            .ToDictionaryAsync(
                session => session.Id,
                session => (session.Title, session.TitleArabic),
                cancellationToken);
        var accounts = await AttendeeAccountIdsAsync(
            released
                .Select(reservation => reservation.ReservedForProfileId!.Value)
                .Distinct()
                .ToList(),
            cancellationToken);

        foreach (var reservation in released)
        {
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.SeatReservationReleased,
                Outcome = AuditOutcome.Success,
                ActorUserId = null, // system-initiated (the pre-start sweep)
                Detail = $"reservationId={reservation.Id}; "
                    + $"sessionId={reservation.SessionId}; "
                    + $"row={reservation.RowLabel}; seat={reservation.SeatNumber}; "
                    + "reason=no-show",
            }, cancellationToken);
            // A holder with no account — a walk-in or a bulk-minted badge — is absent
            // from the map, so there is nobody to tell and the release still stands.
            if (titles.TryGetValue(reservation.SessionId, out var title)
                && accounts.TryGetValue(
                    reservation.ReservedForProfileId!.Value, out var recipientId))
            {
                await NotifyBookingReleasedAsync(
                    reservation, title, recipientId, cancellationToken, noShow: true);
            }
        }

        logger.LogInformation(
            "No-show release freed {Count} un-checked-in seat hold(s).", released.Count);
        return released.Count;
    }

    /// <summary>Read the due holds and release them in ONE serializable
    /// transaction, run through the EF execution strategy because a manual
    /// transaction under <c>EnableRetryOnFailure</c> throws otherwise.
    ///
    /// <para>The "never checked in" test is a NOT EXISTS in the SAME statement that
    /// reads the candidates. It used to be a second query listing every check-in for
    /// every session in play, followed by an in-memory filter, and that shape cost
    /// two things. It left a window: a visitor scanning in at the hall door between
    /// the two steps was still seen as absent, so the sweep freed the seat of
    /// somebody standing in the room and a second visitor could book it. And it grew
    /// without bound: a holder who DID check in keeps <c>ReleasedAt</c> null with a
    /// deadline in the past for ever, so every one of them re-qualified as a
    /// candidate on every 60-second tick and was materialised as a tracked entity
    /// only to be filtered out again — a scan that climbed with cumulative
    /// attendance, dragging a <c>sessionIds.Contains(...)</c> list toward SQL
    /// Server's parameter limit with it. Excluded in the database, a checked-in
    /// holder is never read at all, and a released one drops out for good.</para>
    ///
    /// <para>Serializable makes the decision atomic with the write: the correlated
    /// subquery seeks <c>IX_HallAttendances_SessionId_UserProfileId_Leave</c>, so the
    /// range locks cover only the no-show holders' own keys. A check-in landing
    /// mid-sweep either commits first — and the sweep then sees it and keeps the seat
    /// — or waits out the sweep. It can no longer be lost between the two.</para></summary>
    private async Task<List<SeatReservation>> ReleaseDueNoShowsAsync(
        DateTime now, CancellationToken cancellationToken)
    {
        var released = new List<SeatReservation>();
        var reachedTheWrite = false;
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // A retry re-enters here; drop everything a rolled-back attempt left
            // tracked so the next SaveChanges cannot re-apply a release the database
            // has already thrown away.
            foreach (var stale in released)
            {
                appDbContext.Entry(stale).State = EntityState.Detached;
            }
            released.Clear();

            // ...but a retry does NOT prove the previous attempt rolled back. A
            // transient fault raised BY the commit is ambiguous: the write may have
            // landed and only the acknowledgement been lost. In that case the
            // candidate query below matches nothing, because every row it would have
            // found now carries a ReleasedAt - and the caller would read zero, skip
            // the audit row and send no "your seat was released" notice, while the
            // holders had in fact permanently lost their seats. The stamp is the
            // caller's `now`, so the committed set is recoverable exactly.
            if (reachedTheWrite)
            {
                released.AddRange(await appDbContext.SeatReservations
                    .Where(reservation => reservation.ReleasedAt == now
                        && reservation.Status == BookingStatus.Cancelled
                        && reservation.ReservedForProfileId != null)
                    .ToListAsync(cancellationToken));
                if (released.Count > 0)
                {
                    return; // it committed; hand the caller what actually landed
                }
            }

            await using var tx = await appDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            released.AddRange(await appDbContext.SeatReservations
                .Where(reservation => reservation.Status == BookingStatus.Approved
                    && reservation.ReleasedAt == null
                    && reservation.ReservedForProfileId != null
                    && reservation.NoShowReleaseAt != null
                    && reservation.NoShowReleaseAt <= now
                    && reservation.CreatedAt < reservation.NoShowReleaseAt
                    // The owner rule is "عدم تسجيل الدخول للجلسة" (never checked in),
                    // so ANY HallAttendance row for this (session, holder) keeps the
                    // seat — an arrival that has since left still counts as arrived.
                    && !appDbContext.HallAttendances.Any(
                        attendance => attendance.SessionId == reservation.SessionId
                            && attendance.UserProfileId
                                == reservation.ReservedForProfileId))
                .ToListAsync(cancellationToken));
            if (released.Count == 0)
            {
                return; // nothing due — the transaction rolls back on dispose
            }

            foreach (var reservation in released)
            {
                reservation.ReleasedAt = now;
                reservation.Status = BookingStatus.Cancelled;
            }
            reachedTheWrite = true;
            await appDbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
        return released;
    }

    // -- Staff seating desk --

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
                r.Id, r.Kind, r.Status, r.ReservedForProfileId,
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
            sessionId, held.ReservedForProfileId, cancellationToken);
        return new StaffSeatOccupant(
            true, sessionId, row, seatNumber, tier,
            held.Id, held.Kind, held.Status, occupant.AccountId,
            occupant.Name, occupant.NameArabic,
            held.GuestHint, held.GuestHintArabic,
            occupant.HasPhoto, occupant.QrId, occupant.CheckedIn,
            held.ReservedForProfileId);
    }

    public async Task<bool> EnsureWalkInHoldAsync(
        Guid sessionId, Guid attendeeProfileId, Guid recordedByUserId,
        CancellationToken cancellationToken = default)
    {
        // Already holds a place here — nothing to add. Checked first so the
        // common re-scan case costs one cheap read and never touches the index.
        var alreadyHeld = await appDbContext.SeatReservations
            .AsNoTracking()
            .AnyAsync(
                r => r.SessionId == sessionId
                    && r.ReservedForProfileId == attendeeProfileId
                    && r.ReleasedAt == null,
                cancellationToken);
        if (alreadyHeld) { return false; }

        var hold = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            // Null row + seat: this records THAT they are here, not WHERE. It is
            // also what keeps the hold clear of the per-seat filtered unique
            // index, whose filter requires RowLabel IS NOT NULL.
            RowLabel = null,
            SeatNumber = null,
            Kind = SeatReservationKind.OpenSeating,
            Status = BookingStatus.Approved,
            ReservedForProfileId = attendeeProfileId,
            // The OPERATOR who scanned them in, not the attendee. This column is an
            // Identity account, and a walk-in may hold none — the person who
            // admitted them is both the truthful author and an account that always
            // exists on this path.
            CreatedByUserId = recordedByUserId,
            CreatedAt = timeProvider.SimfNow(),
            // Never expires: the holder is physically in the hall, so the
            // no-show sweep must not release them.
            NoShowReleaseAt = null,
        };

        appDbContext.SeatReservations.Add(hold);
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            // A rival scan won the race for the same attendee, or the store
            // rejected the insert. Either way the person is already through the
            // door: detach and report that nothing was added, rather than
            // throwing into an admission that has already happened.
            appDbContext.Entry(hold).State = EntityState.Detached;
            logger.LogWarning(
                ex,
                "Walk-in seat hold not recorded for attendee {AttendeeProfileId} at session {SessionId}.",
                attendeeProfileId, sessionId);
            return false;
        }
    }

    public async Task<StaffSeatOccupant> ResolveBadgeSeatAsync(
        Guid sessionId, string qrId, CancellationToken cancellationToken = default)
    {
        // Canonicalise first. An offline badge arrives as a
        // ~61-character encrypted blob, not a QrId, so the direct lookup below
        // would miss it and report an unknown badge. A minted serial passes
        // through unchanged.
        var code = qrResolver.ToStoredQrId(qrId ?? string.Empty);
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

        // The badge resolves to the attendee PROFILE, which every holder has and
        // which the reservation is keyed by. This used to resolve the Identity
        // account instead, and a walk-in — who has none — could not be looked up at
        // all: the desk answered "no seat" for the very badge the walk-in hold had
        // just been created for.
        var holder = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.QrId == code)
            .Select(p => new { p.Id, p.Name, p.NameArabic })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AttendeeQrUnknown, 404,
                "That badge was not recognised.",
                "لم يتم التعرف على هذه البطاقة.");

        var held = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId
                && r.ReservedForProfileId == holder.Id
                && r.ReleasedAt == null)
            .Select(r => new
            {
                r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
                r.GuestHint, r.GuestHintArabic,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var occupant = await LoadOccupantAsync(
            sessionId, holder.Id, cancellationToken);
        if (held is null)
        {
            // The badge is valid but the guest holds no seat in this session — the
            // desk shows the "no seat" state with the guest's identity so staff can
            // still help them (Found = false).
            return new StaffSeatOccupant(
                false, sessionId, null, null, SeatTier.Normal,
                null, SeatReservationKind.UserBooking, BookingStatus.Cancelled,
                occupant.AccountId, occupant.Name, occupant.NameArabic,
                null, null, occupant.HasPhoto, code, occupant.CheckedIn,
                holder.Id);
        }

        var tierIndex = held.RowLabel is null
            ? -1
            : RowIndex(rowLabels, held.RowLabel);
        var tier = tierIndex >= 0 && tierIndex < tiers.Count
            ? tiers[tierIndex]
            : SeatTier.Normal;
        return new StaffSeatOccupant(
            true, sessionId, held.RowLabel, held.SeatNumber, tier,
            held.Id, held.Kind, held.Status, occupant.AccountId,
            occupant.Name, occupant.NameArabic,
            held.GuestHint, held.GuestHintArabic,
            occupant.HasPhoto, code, occupant.CheckedIn,
            holder.Id);
    }

    private static StaffSeatOccupant EmptySeat(
        Guid sessionId, string rowLabel, int seatNumber, SeatTier tier) =>
        new(false, sessionId, rowLabel, seatNumber, tier,
            null, SeatReservationKind.UserBooking, BookingStatus.Cancelled,
            null, string.Empty, string.Empty, null, null, false, null, false, null);

    /// <summary>The occupant facts the seating desk shows: bilingual name +
    /// badge id (from the App-side <c>UserProfile</c>), whether an avatar exists in
    /// the unified file store, and whether they have already checked into this
    /// session. Everything is on the App DB, so there is no cross-database read and
    /// nothing is duplicated. A null <paramref name="attendeeProfileId"/> (a VVIP
    /// protocol seat or an admin block) yields the empty occupant.
    ///
    /// <para>Also returns the occupant's ACCOUNT id, which the shipped
    /// <c>StaffSeatOccupant.UserId</c> field carries and the avatar route keys on.
    /// It is null for a walk-in or a bulk-minted badge, whose name and seat still
    /// resolve from the profile — only the photo has nowhere to come from.</para></summary>
    private async Task<(string Name, string NameArabic, bool HasPhoto,
        string? QrId, bool CheckedIn, Guid? AccountId)> LoadOccupantAsync(
        Guid sessionId, Guid? attendeeProfileId, CancellationToken cancellationToken)
    {
        if (attendeeProfileId is not { } id)
        {
            return (string.Empty, string.Empty, false, null, false, null);
        }
        var profile = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Name, p.NameArabic, p.QrId, p.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        // An avatar is owned by an ACCOUNT, so an attendee without one can have no
        // photo; skipping the query is also one round-trip saved on every walk-in.
        var hasPhoto = profile?.UserId is { } avatarOwnerId
            && await appDbContext.StoredFiles.AsNoTracking()
                .AnyAsync(f => f.Service == FileService.Avatar
                    && f.OwnerEntityId == avatarOwnerId
                    && f.IsActive, cancellationToken);
        var checkedIn = await appDbContext.HallAttendances.AsNoTracking()
            .AnyAsync(a => a.SessionId == sessionId && a.UserProfileId == id,
                cancellationToken);
        return (profile?.Name ?? string.Empty, profile?.NameArabic ?? string.Empty,
            hasPhoto, profile?.QrId, checkedIn, profile?.UserId);
    }

    // -- internals --

    private Task<(string Title, string TitleArabic)> LoadSessionTitleAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new ValueTuple<string, string>(s.Title, s.TitleArabic))
            .SingleAsync(cancellationToken);

    private async Task EnsureNoOverlapAsync(
        Guid sessionId, Guid actorProfileId,
        DateTime start, DateTime end,
        CancellationToken cancellationToken)
    {
        // The attendee must not already hold a (Pending or Approved)
        // booking for ANOTHER session whose time window overlaps this one.
        // Held = ReleasedAt IS NULL, so released/rejected/cancelled rows don't
        // block.
        var overlaps = await appDbContext.SeatReservations.AsNoTracking()
            .Where(reservation => reservation.ReservedForProfileId == actorProfileId
                && reservation.ReleasedAt == null
                && reservation.SessionId != sessionId)
            .Join(appDbContext.Sessions.AsNoTracking(),
                reservation => reservation.SessionId,
                session => session.Id,
                (reservation, session) => new { session.Start, session.End })
            .AnyAsync(window => window.Start < end && start < window.End,
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
            session.Id, session.CapacityOverride, hall.Capacity, rowLabels,
            session.Start, session.End,
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

    /// <summary>Reject a hall-layout change that would orphan any active
    /// (ReleasedAt IS NULL) seat-specific reservation across the hall's sessions:
    /// a booked row no longer in <paramref name="newRows"/>, or a seat number
    /// above that row's new per-row count in <paramref name="newSeatCounts"/>.
    /// Open-seating reservations (null row/seat) are unaffected. The operator must
    /// release the affected seats before shrinking the grid.</summary>
    private async Task EnsureLayoutChangeKeepsActiveReservationsAsync(
        Guid hallId, IReadOnlyList<string> newRows, IReadOnlyList<int> newSeatCounts,
        CancellationToken cancellationToken)
    {
        var sessionIds = await HallSessionIdsAsync(hallId, cancellationToken);
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

        var orphaned = activeSeats.Any(seat =>
        {
            var rowIndex = RowIndex(newRows, seat.RowLabel!);
            return rowIndex < 0 || (seat.SeatNumber ?? 0) > newSeatCounts[rowIndex];
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

    /// <summary>How many ACTIVE (ReleasedAt IS NULL) seat-SPECIFIC reservations
    /// exist across every session in this hall. Open-seating rows (null row label)
    /// survive a layout removal untouched — general admission needs no grid — so they
    /// are excluded. Counted in the database (no rows materialised) because the caller
    /// only needs the number to put in the refusal message.</summary>
    private async Task<int> CountActiveSeatReservationsAsync(
        Guid hallId, CancellationToken cancellationToken)
    {
        var sessionIds = await HallSessionIdsAsync(hallId, cancellationToken);
        if (sessionIds.Count == 0)
        {
            return 0;
        }
        return await appDbContext.SeatReservations.AsNoTracking()
            .CountAsync(r => sessionIds.Contains(r.SessionId)
                && r.ReleasedAt == null
                && r.RowLabel != null, cancellationToken);
    }

    /// <summary>Every session held in this hall that has NOT yet ended. One
    /// definition shared by the two layout guards (the shrink orphan check and the
    /// layout-delete count) so they can never disagree on which sessions a layout
    /// change affects.
    ///
    /// <para>The end bound is the guard, not a tidy-up. Nothing stamps
    /// <c>ReleasedAt</c> when a session merely finishes — only a cancel, an admin
    /// release, a seat move, a session cancellation and the no-show sweep do — so
    /// every attendee who actually turned up leaves an active row behind for ever.
    /// Without the bound, one 300-seat session held last year makes the hall's grid
    /// permanently un-editable: the next edition's admin cannot rename, drop or
    /// shrink a row without first releasing hundreds of holds for a session that
    /// finished months ago. A seat in a session that has ended can never be sat in,
    /// so it is not a reservation a layout change can strand. A session that has
    /// merely STARTED still counts — its attendees are in the room.</para></summary>
    private Task<List<Guid>> HallSessionIdsAsync(
        Guid hallId, CancellationToken cancellationToken)
    {
        var now = timeProvider.SimfNow();
        return appDbContext.Sessions.AsNoTracking()
            .Where(s => s.HallId == hallId && s.End > now)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<string> ParseRowLabels(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

    /// <summary>Expand a layout's per-row seat counts into a concrete array
    /// parallel to <paramref name="rowLabels"/>. When <c>SeatCounts</c> is null/blank the
    /// layout is uniform, so every row gets <c>SeatsPerRow</c> (the behaviour before
    /// per-row counts existed); when set it is a CSV of ints, one per row. A stored
    /// CSV whose length differs from the row set, or that fails to parse, is corrupt
    /// persisted state — a deterministic 500, never a silent fallback, per the
    /// project's no-silent-fallback rule.</summary>
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

    /// <summary>Expand a layout's per-row seat TIERS into a concrete array
    /// parallel to <paramref name="rowLabels"/>. A null/blank <c>SeatTiers</c> is a
    /// layout written before seat tiers existed, so every row reads
    /// <see cref="SeatTier.Normal"/> — exactly the behaviour those layouts already
    /// had, so no shipped session loses a bookable seat. A stored CSV whose length
    /// differs from the row set, or that fails to parse into a defined tier, is
    /// corrupt persisted state — a deterministic 500, never a silent fallback, per
    /// the project's no-silent-fallback rule.</summary>
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

    /// <summary>The eligibility rule, in ONE place so the self-pick, the
    /// random pick and the seat map can never disagree (owner 2026-07-26):
    /// <list type="bullet">
    /// <item><see cref="SeatTier.Vvip"/> — never self-reservable by anyone. There is
    /// no registration for a protocol seat; an administrator blocks it and types the
    /// guest hint.</item>
    /// <item><see cref="SeatTier.Vip"/> — only a VIP-tier visitor (their
    /// <c>ProfileType.IsVipTier</c>, the seeded VVIP + VIP rows and the
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

    /// <summary>Throw the caller-facing eligibility error for the seat's
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

    /// <summary>Is this visitor a VIP-tier attendee? Reuses the EXISTING
    /// VIP-tier notion rather than inventing a parallel one:
    /// <c>UserProfile.ProfileTypeId → UserProfileType.IsVipTier</c>, which
    /// the seeder sets on the VVIP + VIP audience tiers and the app already
    /// surfaces as <c>isVip</c>. Both tables live on the App DB, so this is a
    /// single local query — no cross-database read.</summary>
    private async Task<bool> IsVipVisitorAsync(
        Guid actorUserId, CancellationToken cancellationToken) =>
        await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == actorUserId
                && profile.ProfileTypeId != null)
            .Join(appDbContext.ProfileTypes.AsNoTracking(),
                profile => profile.ProfileTypeId,
                profileType => (Guid?)profileType.Id,
                (profile, profileType) => profileType.IsVipTier)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Index of <paramref name="label"/> within
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
        // The seat-pick paths are only for assigned-seat sessions; an
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
        // Bound the seat against THIS row's count (ctx.SeatCounts[i]), not a
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

    /// <summary>A booking may still be created on
    /// a live, in-progress session (a walk-in can join), but NOT on one that has
    /// already ENDED: an ended session's seat can never be attended, so the hold would
    /// be dead, un-cancellable weight. Blocks at or after <paramref name="end"/>;
    /// a merely-started (not yet ended) session stays bookable.</summary>
    private void EnsureSessionNotEnded(DateTime end)
    {
        if (timeProvider.SimfNow() >= end)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionEnded, 409,
                "This session has ended; you can no longer book a seat.",
                "انتهت هذه الجلسة، ولم يعد بإمكانك حجز مقعد.");
        }
    }

    /// <summary>The self-service seat CHANGE window: only BEFORE the session
    /// starts. Deliberately the same boundary <see cref="ReleaseMineAsync"/> uses for
    /// a cancel, rather than the looser not-yet-ENDED rule the create
    /// paths use: a walk-in may still book a live session, but reshuffling an
    /// already-placed attendee mid-session would desync the staff seating desk and the
    /// pre-start no-show sweep that has already redistributed the free seats.</summary>
    private void EnsureSessionNotStarted(DateTime start)
    {
        if (timeProvider.SimfNow() >= start)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionStarted, 409,
                "You cannot change your seat after the session has started.",
                "لا يمكنك تغيير مقعدك بعد بدء الجلسة.");
        }
    }

    /// <summary>The session's effective place count: the seat-layout total
    /// Sum of the per-row seat counts — equal to rows × seatsPerRow when the
    /// layout is uniform) capped by the smaller of Session.CapacityOverride and
    /// Hall.Capacity. One definition shared by the reserve pre-check and the
    /// post-insert backstop so they can never disagree.</summary>
    private static int EffectiveCapacity(SessionContext ctx) =>
        Math.Min(
            ctx.SeatCounts.Sum(),
            ctx.CapacityOverride ?? ctx.HallCapacity);

    /// <summary>Refuse a second live hold for an attendee who already holds one in
    /// this session — the rule the filtered unique index on
    /// (SessionId, ReservedForProfileId) enforces, answered as a 409 instead of a
    /// duplicate-key 500. Called TWICE on every create path: once up front, to fail
    /// a double-tap cheaply, and once more inside the serializable transaction that
    /// does the insert, where it is the check that actually holds.
    /// <paramref name="openSeating"/> selects the wording only — an open-seating
    /// join has no seat to speak of, so it says "booking" where a seat pick says
    /// "seat reserved"; the error code is the same on both.</summary>
    private async Task EnsureNoActiveHoldAsync(
        Guid sessionId, Guid actorProfileId, bool openSeating,
        CancellationToken cancellationToken)
    {
        var existing = await GetMyActiveAsync(sessionId, actorProfileId, cancellationToken);
        if (existing is null)
        {
            return;
        }
        throw new ApiException(
            ErrorCodes.SeatAlreadyOwnedBySession, 409,
            openSeating
                ? "You already have a booking for this session."
                : "You already have a seat reserved for this session.",
            openSeating
                ? "لديك حجز بالفعل لهذه الجلسة."
                : "لديك مقعد محجوز بالفعل لهذه الجلسة.");
    }

    /// <summary>Refuse a seat somebody already holds — a visitor's booking or an
    /// administrator's protocol block, which are the same thing to this check.
    /// Called up front to fail cheaply, and again inside the serializable
    /// transaction that inserts, where its key-range lock is what stops a rival
    /// slipping in between the read and the write.</summary>
    private async Task EnsureSeatIsFreeAsync(
        Guid sessionId, string rowLabel, int seatNumber,
        CancellationToken cancellationToken)
    {
        var taken = await appDbContext.SeatReservations.AsNoTracking()
            .AnyAsync(r => r.SessionId == sessionId
                && r.RowLabel == rowLabel
                && r.SeatNumber == seatNumber
                && r.ReleasedAt == null, cancellationToken);
        if (taken)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyReserved, 409,
                "That seat is already reserved.",
                "هذا المقعد محجوز بالفعل.");
        }
    }

    /// <summary>Insert a hold only while the
    /// session is below <paramref name="effectiveCap"/>, with the capacity COUNT and
    /// the INSERT in ONE SERIALIZABLE transaction so concurrent bookings — a seat
    /// pick, a reserve-random or an open-seating join, all three go through here —
    /// can neither oversell nor over-reject. The COUNT takes a
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
            //
            // First establish WHICH kind of retry this is. A dropped connection
            // between SQL Server's commit and the acknowledgement reaching us looks
            // transient, so the strategy re-runs a block whose row is already stored;
            // re-inserting it then violates the filtered unique index on
            // (SessionId, ReservedForProfileId) with a duplicate key, which is NOT
            // transient, so a raw DbUpdateException escapes as a 500 over a booking
            // that in fact succeeded. The id is client-generated, so nobody else could
            // have written it: finding it means the attempt committed, and the answer
            // is that row. Detaching is load-bearing on that path too — an entity left
            // Added would be re-inserted by the next SaveChanges on this context.
            committed = null;
            var previous = added;
            var recovered = false;
            if (previous is not null)
            {
                var previousId = previous.Id;
                recovered = await appDbContext.SeatReservations.AsNoTracking()
                    .AnyAsync(r => r.Id == previousId, cancellationToken);
                appDbContext.Entry(previous).State = EntityState.Detached;
                added = null;
            }
            if (recovered)
            {
                committed = previous;
                return;
            }

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

    /// <summary>The session's currently-held seat-specific cells (row + number),
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
            .Select(cell => (Row: cell.RowLabel!, Seat: cell.SeatNumber!.Value))
            .ToHashSet();
    }

    /// <summary>The first free seat (row-major over the layout) as a fresh
    /// confirmed RandomAssignment hold, or null when every seat is taken. Built with
    /// the captured <paramref name="now"/> so a transaction retry stamps the same
    /// created-at / expiry window.</summary>
    private static SeatReservation? PickRandomSeat(
        SessionContext ctx, IReadOnlySet<(string Row, int Seat)> taken,
        Guid actorProfileId, Guid actorUserId, DateTime now, bool callerIsVip)
    {
        // Index loop so each row's free-seat scan stops at ITS own count
        // (ctx.SeatCounts[i]); a ragged layout never yields a phantom seat on a short row.
        for (var i = 0; i < ctx.RowLabels.Count; i++)
        {
            // Skip whole rows the caller may not sit in (a VVIP protocol row,
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
                    ReservedForProfileId = actorProfileId,
                    CreatedByUserId = actorUserId,
                    CreatedAt = now,
                    // 2026-07-18 (reservation-only) — confirmed on create, no approval.
                    Status = BookingStatus.Approved,
                    // The no-show release deadline: 3 minutes before start.
                    NoShowReleaseAt = ctx.Start - NoShowReleaseGrace,
                };
            }
        }
        return null;
    }

    private Task<SeatReservation?> GetMyActiveAsync(
        Guid sessionId, Guid actorProfileId, CancellationToken cancellationToken) =>
        appDbContext.SeatReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.SessionId == sessionId
                && r.ReservedForProfileId == actorProfileId
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
            if (message.Contains("ReservedForProfileId", StringComparison.OrdinalIgnoreCase))
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

    // Notify the attendee that their seat reservation was released:
    // either by an administrator (default) or by the pre-start no-show sweep
    // (noShow=true, so the message explains they were not checked in). Same
    // swallow-and-log discipline as the other booking notifications: a
    // notification failure never rolls back the release (the dispatcher writes to
    // the Identity DB, already committed here).
    private async Task TryNotifyBookingReleasedAsync(
        SeatReservation booking, (string Title, string TitleArabic) session,
        CancellationToken cancellationToken, bool noShow = false)
    {
        if (booking.ReservedForProfileId is not { } holderProfileId)
        {
            return;
        }

        // A notification is delivered to an ACCOUNT — it owns the devices and the
        // mailbox — while the booking is held by an attendee PROFILE. A walk-in
        // holds a seat and no account, so there is nobody to tell; their release
        // still stands, exactly as an admin block's silent release does.
        var userId = await AttendeeAccountIdAsync(holderProfileId, cancellationToken);
        if (userId is not { } recipientId)
        {
            return;
        }
        await NotifyBookingReleasedAsync(
            booking, session, recipientId, cancellationToken, noShow);
    }

    /// <summary>Compose and dispatch the release message to an already-resolved
    /// recipient. Split from <see cref="TryNotifyBookingReleasedAsync"/> so the sweep,
    /// which resolves every holder's account in one query, does not pay a lookup per
    /// freed seat.</summary>
    private async Task NotifyBookingReleasedAsync(
        SeatReservation booking, (string Title, string TitleArabic) session,
        Guid recipientId, CancellationToken cancellationToken, bool noShow)
    {
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
                UserId = recipientId,
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

    private static MySeatReservation ToMine(SeatReservation reservation) =>
        new(reservation.Id, reservation.SessionId, reservation.RowLabel,
            reservation.SeatNumber, reservation.Kind, reservation.CreatedAt,
            reservation.Status);

    private sealed record SessionSnapshot(
        Guid Id, Guid HallId, int? CapacityOverride, string Title, string TitleArabic,
        DateTime Start, DateTime End,
        SeatSelectionMode? SeatSelectionModeOverride);
    private sealed record SessionContext(
        Guid SessionId, int? CapacityOverride, int HallCapacity,
        IReadOnlyList<string> RowLabels,
        DateTime Start, DateTime End,
        SeatSelectionMode EffectiveMode,
        // The expanded per-row seat counts (one per RowLabels entry; a repeat of
        // SeatsPerRow when the layout is uniform). Every per-seat bound/capacity/random-
        // pick decision reads this array so uniform and variable layouts share one path.
        IReadOnlyList<int> SeatCounts,
        // The expanded per-row seat TIERS (one per RowLabels entry; all Normal
        // for a legacy layout that stores none). Every eligibility decision reads this
        // array so the self-pick, the random pick and the seat map can never disagree.
        IReadOnlyList<SeatTier> SeatTiers);
}
