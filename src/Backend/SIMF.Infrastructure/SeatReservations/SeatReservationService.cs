// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
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
    /// <summary>M-6 — how long a Pending, unapproved visitor booking holds its
    /// seat before the expiry worker releases it. Defined here (not on the worker)
    /// so the stamp written at creation and the scan that reads it share one
    /// source.</summary>
    internal static readonly TimeSpan PendingHoldWindow = TimeSpan.FromHours(24);

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
            })
            .ToListAsync(cancellationToken);

        // Wave 2 — the "confirmed" (تم التأكيد) seat state: a reservation whose
        // holder has an OPEN HallAttendance row for this session (scanned in at the
        // hall gate). One query for the whole session, matched by holder id.
        var checkedInUserIds = (await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.LeaveUtc == null)
            .Select(a => a.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var cells = reservations.Select(r => new SessionSeatCell(
            r.Id, r.RowLabel, r.SeatNumber, r.Kind, r.Status,
            r.ReservedForUserId is { } holder && checkedInUserIds.Contains(holder)))
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
                    ownRow.ReservedForUserId is { } m && checkedInUserIds.Contains(m));
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

        return new SessionSeatMap(
            sessionId, session.HallId, hall.Capacity, session.CapacityOverride,
            rowLabels, layout?.SeatsPerRow ?? 0,
            cells, mine, cells.Count,
            // D-432 — the session title is already loaded in the snapshot.
            session.Title, session.TitleArabic,
            // D-485 — the effective mode drives the app's Join CTA.
            effectiveMode);
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
        EnsureSessionNotEnded(ctx.EndUtc);
        ValidateSeatBounds(ctx, row, seat);
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
            sessionId, actorUserId, ctx.StartUtc, ctx.EndUtc, cancellationToken);

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
            // M-6 — retained as the hold's outstanding-until marker.
            ExpiresUtc = timeProvider.GetUtcNow() + PendingHoldWindow,
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
        EnsureSessionNotEnded(ctx.EndUtc);

        var existing = await GetMyActiveAsync(sessionId, actorUserId, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyOwnedBySession, 409,
                "You already have a seat reserved for this session.",
                "لديك مقعد محجوز بالفعل لهذه الجلسة.");
        }

        await EnsureNoOverlapAsync(
            sessionId, actorUserId, ctx.StartUtc, ctx.EndUtc, cancellationToken);

        // M-2 / #21 — the capacity COUNT, the free-seat pick and the INSERT run in
        // ONE Serializable transaction so concurrent reserve-random can neither
        // oversell (the key-range lock serialises count-then-insert) nor over-reject
        // (a deadlock victim re-runs and its re-count sees the committed rival),
        // filling exactly the declared capacity. See InsertHoldWithinCapacityAsync.
        var now = timeProvider.GetUtcNow();
        var reservation = await InsertHoldWithinCapacityAsync(
            sessionId, EffectiveCapacity(ctx),
            async ct =>
            {
                var taken = await LoadHeldSeatsAsync(sessionId, ct);
                return PickRandomSeat(ctx, taken, actorUserId, now);
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

        EnsureSessionNotEnded(session.EndUtc);

        var existing = await GetMyActiveAsync(sessionId, actorUserId, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyOwnedBySession, 409,
                "You already have a booking for this session.",
                "لديك حجز بالفعل لهذه الجلسة.");
        }

        await EnsureNoOverlapAsync(
            sessionId, actorUserId, session.StartUtc, session.EndUtc, cancellationToken);

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
                // M-6 — retained as the hold's outstanding-until marker.
                ExpiresUtc = now + PendingHoldWindow,
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
        var startUtc = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => (DateTimeOffset?)s.StartUtc)
            .SingleOrDefaultAsync(cancellationToken);
        if (startUtc is { } start && timeProvider.GetUtcNow() >= start)
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
        return new HallSeatLayoutSnapshot(
            hallId, rowLabels, seatsPerRow,
            rowLabels.Count * seatsPerRow, hall.Capacity);
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
        if (request.SeatsPerRow is < 1 or > 80)
        {
            throw new ApiException(
                ErrorCodes.SeatLayoutInvalid, 400,
                "Seats per row must be between 1 and 80.",
                "يجب أن يكون عدد المقاعد في كل صف بين 1 و 80.");
        }

        var layoutCapacity = rows.Count * request.SeatsPerRow;
        if (layoutCapacity > hall.Capacity)
        {
            throw new ApiException(
                ErrorCodes.SeatCapacityExceeded, 400,
                $"Layout capacity ({layoutCapacity}) exceeds hall capacity ({hall.Capacity}).",
                $"السعة المقترحة ({layoutCapacity}) تتجاوز سعة القاعة ({hall.Capacity}).");
        }

        var layout = await appDbContext.HallSeatLayouts
            .SingleOrDefaultAsync(l => l.HallId == hallId, cancellationToken);
        // H-2 — an existing layout may already back active reservations; a change
        // that drops a row or shrinks the seats-per-row would strand any seat that
        // now falls outside the grid. Block it (the operator must release those
        // seats first). A first-time layout (layout is null) can have no seat-
        // specific reservations yet — the reserve paths require a layout.
        if (layout is not null)
        {
            await EnsureLayoutChangeKeepsActiveReservationsAsync(
                hallId, rows, request.SeatsPerRow, cancellationToken);
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
                SeatsPerRow = request.SeatsPerRow,
                CreatedAt = now,
            };
            appDbContext.HallSeatLayouts.Add(layout);
        }
        else
        {
            layout.RowLabels = rowsCsv;
            layout.SeatsPerRow = request.SeatsPerRow;
            layout.UpdatedAt = now;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallSeatLayoutUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"hallId={hallId}; rows={rowsCsv}; seatsPerRow={request.SeatsPerRow}",
        }, cancellationToken);

        return new HallSeatLayoutSnapshot(
            hallId, rows, request.SeatsPerRow, layoutCapacity, hall.Capacity);
    }

    public async Task AdminReserveRowAsync(
        Guid actorUserId, Guid sessionId,
        AdminReserveRowRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        if (!ctx.RowLabels.Contains(row, StringComparer.OrdinalIgnoreCase))
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
        for (var seat = 1; seat <= ctx.Layout!.SeatsPerRow; seat++)
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
        };
        await PersistWithUniquenessGuardAsync(reservation, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatRowAdminReserved,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; row={row}; seat={seat}; single=true",
        }, cancellationToken);
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
        // Cancelled and stamp the reviewer, mirroring RejectBookingAsync.
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

    // -- Booking approval queue (P2.2 / D-227 — FDS-005 §5.2) --

    public async Task<GridPage<BookingQueueRow>> ListPendingBookingsAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

        // Pending, still-held visitor bookings. Admin row-blocks are created
        // Approved with a null ReservedForUserId, so they never appear here.
        // The session is joined up-front (before paging) so the session and
        // seat columns are server-filterable/sortable (D-255). The attendee
        // name is resolved cross-DB from Identity afterwards, so that column
        // stays non-filterable/non-sortable (D-157).
        var joined = appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.Status == BookingStatus.Pending
                && r.ReleasedAt == null
                && r.ReservedForUserId != null)
            .Join(appDbContext.Sessions.AsNoTracking(),
                r => r.SessionId, s => s.Id,
                (r, s) => new
                {
                    r.Id, r.SessionId, s.Title, s.TitleArabic, s.StartUtc,
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
            ("start", false) => joined.OrderBy(x => x.StartUtc),
            ("start", true) => joined.OrderByDescending(x => x.StartUtc),
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
            return new BookingQueueRow(
                r.Id, r.SessionId, r.Title, r.TitleArabic, r.StartUtc,
                r.RowLabel, r.SeatNumber, r.Kind, r.ReservedForUserId,
                attendeeName, r.CreatedAt);
        }).ToList();

        return GridPage<BookingQueueRow>.Of(items, total,
            skip, top);
    }

    public async Task ApproveBookingAsync(
        Guid actorUserId, Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadPendingBookingAsync(reservationId, cancellationToken);

        // M-1 — a seat is only HELD while Pending; confirming it must not push the
        // session's CONFIRMED count past capacity. Re-check here because a race
        // (open seating especially) can let more Pending holds accumulate than
        // there are places. Overflow blocks the approval (the booking stays Pending
        // so the admin can reject it explicitly). This is the CP approval backstop
        // that previously did not exist.
        await EnsureApprovalWithinCapacityAsync(booking, cancellationToken);

        booking.Status = BookingStatus.Approved;
        booking.ReviewedByUserId = actorUserId;
        booking.ReviewedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BookingApproved,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={booking.Id}; sessionId={booking.SessionId}; "
                + $"row={booking.RowLabel}; seat={booking.SeatNumber}",
        }, cancellationToken);

        var session = await LoadSessionTitleAsync(booking.SessionId, cancellationToken);
        await TryNotifyBookingConfirmedAsync(booking, session, cancellationToken);
    }

    public async Task RejectBookingAsync(
        Guid actorUserId, Guid reservationId, RejectBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length is < 1 or > 512)
        {
            throw new ApiException(
                ErrorCodes.BookingRejectionReasonRequired, 400,
                "A reason is required to reject a booking (1–512 characters).",
                "يلزم إدخال سبب لرفض الحجز (من 1 إلى 512 حرفاً).");
        }

        var booking = await LoadPendingBookingAsync(reservationId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        booking.Status = BookingStatus.Rejected;
        booking.RejectionReason = reason;
        booking.ReviewedByUserId = actorUserId;
        booking.ReviewedAt = now;
        booking.ReleasedAt = now; // release the held seat
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BookingRejected,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={booking.Id}; sessionId={booking.SessionId}; "
                + $"row={booking.RowLabel}; seat={booking.SeatNumber}",
        }, cancellationToken);

        var session = await LoadSessionTitleAsync(booking.SessionId, cancellationToken);
        await TryNotifyBookingRejectedAsync(booking, session, reason, cancellationToken);
    }

    public async Task<int> BulkApproveBookingsAsync(
        Guid actorUserId, IReadOnlyList<Guid> reservationIds,
        CancellationToken cancellationToken = default)
    {
        if (reservationIds is null || reservationIds.Count == 0)
        {
            return 0;
        }

        var distinctIds = reservationIds.Distinct().ToList();
        var approved = 0;
        foreach (var id in distinctIds)
        {
            var booking = await appDbContext.SeatReservations
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (booking is null
                || booking.Status != BookingStatus.Pending
                || booking.ReleasedAt is not null)
            {
                continue; // skip missing / already-decided
            }
            // M-1 — skip (do not approve) any booking that would overflow the
            // session's confirmed capacity. The sequential loop re-checks after each
            // committed approval, so it naturally stops filling a session once full,
            // matching the single-approve gate.
            if (await WouldApprovalOverflowAsync(booking, cancellationToken))
            {
                continue;
            }
            booking.Status = BookingStatus.Approved;
            booking.ReviewedByUserId = actorUserId;
            booking.ReviewedAt = timeProvider.GetUtcNow();
            await appDbContext.SaveChangesAsync(cancellationToken);

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.BookingApproved,
                Outcome = AuditOutcome.Success,
                ActorUserId = actorUserId,
                Detail = $"reservationId={booking.Id}; sessionId={booking.SessionId}; "
                    + $"row={booking.RowLabel}; seat={booking.SeatNumber}; bulk=true",
            }, cancellationToken);

            var session = await LoadSessionTitleAsync(booking.SessionId, cancellationToken);
            await TryNotifyBookingConfirmedAsync(booking, session, cancellationToken);
            approved++;
        }
        return approved;
    }

    // -- internals --

    private async Task<SeatReservation> LoadPendingBookingAsync(
        Guid reservationId, CancellationToken cancellationToken)
    {
        var booking = await appDbContext.SeatReservations
            .SingleOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BookingNotFound, 404,
                "The booking was not found.",
                "لم يتم العثور على الحجز.");
        if (booking.Status != BookingStatus.Pending || booking.ReleasedAt is not null)
        {
            throw new ApiException(
                ErrorCodes.BookingNotPending, 409,
                "This booking has already been decided.",
                "تم البت في هذا الحجز بالفعل.");
        }
        return booking;
    }

    /// <summary>M-1 — true if confirming <paramref name="booking"/> would push the
    /// session's CONFIRMED (Approved, still-held) count past its effective
    /// capacity. Counts Approved holds only (Pending holds are provisional); the
    /// booking itself is excluded. Effective capacity = the seat-layout total
    /// capped by CapacityOverride/Hall.Capacity, or just the declared cap for an
    /// open-seating session with no layout.</summary>
    private async Task<bool> WouldApprovalOverflowAsync(
        SeatReservation booking, CancellationToken cancellationToken)
    {
        var session = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == booking.SessionId)
            .Select(s => new { s.HallId, s.CapacityOverride })
            .SingleAsync(cancellationToken);
        var hallCapacity = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => h.Capacity)
            .SingleAsync(cancellationToken);
        var layout = await appDbContext.HallSeatLayouts.AsNoTracking()
            .Where(l => l.HallId == session.HallId)
            .Select(l => new { l.RowLabels, l.SeatsPerRow })
            .SingleOrDefaultAsync(cancellationToken);

        var declaredCap = session.CapacityOverride ?? hallCapacity;
        var effective = layout is not null && layout.SeatsPerRow > 0
            ? Math.Min(ParseRowLabels(layout.RowLabels).Count * layout.SeatsPerRow, declaredCap)
            : declaredCap;

        var approvedActive = await appDbContext.SeatReservations
            .Where(r => r.SessionId == booking.SessionId
                && r.ReleasedAt == null
                && r.Status == BookingStatus.Approved
                && r.Id != booking.Id)
            .CountAsync(cancellationToken);
        return approvedActive >= effective;
    }

    private async Task EnsureApprovalWithinCapacityAsync(
        SeatReservation booking, CancellationToken cancellationToken)
    {
        if (await WouldApprovalOverflowAsync(booking, cancellationToken))
        {
            throw new ApiException(
                ErrorCodes.SeatSessionFull, 409,
                "Approving this booking would exceed the session capacity.",
                "الموافقة على هذا الحجز تتجاوز سعة الجلسة.");
        }
    }

    private Task<(string Title, string TitleArabic)> LoadSessionTitleAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new ValueTuple<string, string>(s.Title, s.TitleArabic))
            .SingleAsync(cancellationToken);

    private async Task EnsureNoOverlapAsync(
        Guid sessionId, Guid actorUserId,
        DateTimeOffset startUtc, DateTimeOffset endUtc,
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
                r => r.SessionId, s => s.Id, (r, s) => new { s.StartUtc, s.EndUtc })
            .AnyAsync(x => x.StartUtc < endUtc && startUtc < x.EndUtc,
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
        return new SessionContext(
            session.Id, session.HallId, session.CapacityOverride,
            hall.Capacity, layout, ParseRowLabels(layout.RowLabels),
            session.Title, session.TitleArabic, session.StartUtc, session.EndUtc,
            effectiveMode);
    }

    private async Task<SessionSnapshot> LoadSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        return await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new SessionSnapshot(
                s.Id, s.HallId, s.CapacityOverride, s.Title, s.TitleArabic,
                s.StartUtc, s.EndUtc, s.SeatSelectionModeOverride))
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
    /// a booked row no longer in <paramref name="newRows"/>, or a seat number above
    /// <paramref name="newSeatsPerRow"/>. Open-seating reservations (null row/seat)
    /// are unaffected. The operator must release the affected seats before
    /// shrinking the grid.</summary>
    private async Task EnsureLayoutChangeKeepsActiveReservationsAsync(
        Guid hallId, IReadOnlyList<string> newRows, int newSeatsPerRow,
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

        var allowedRows = new HashSet<string>(newRows, StringComparer.OrdinalIgnoreCase);
        var orphaned = activeSeats.Any(s =>
            !allowedRows.Contains(s.RowLabel!)
            || (s.SeatNumber ?? 0) > newSeatsPerRow);
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
        if (string.IsNullOrEmpty(rowLabel)
            || !ctx.RowLabels.Contains(rowLabel, StringComparer.OrdinalIgnoreCase))
        {
            throw new ApiException(
                ErrorCodes.SeatOutOfBounds, 400,
                $"Row '{rowLabel}' is not in the hall layout.",
                $"الصف '{rowLabel}' غير موجود في مخطط القاعة.");
        }
        if (seatNumber < 1 || seatNumber > ctx.Layout!.SeatsPerRow)
        {
            throw new ApiException(
                ErrorCodes.SeatOutOfBounds, 400,
                $"Seat number must be between 1 and {ctx.Layout.SeatsPerRow}.",
                $"يجب أن يكون رقم المقعد بين 1 و {ctx.Layout.SeatsPerRow}.");
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
    /// be dead, un-cancellable weight. Blocks at or after <paramref name="endUtc"/>;
    /// a merely-started (not yet ended) session stays bookable.</summary>
    private void EnsureSessionNotEnded(DateTimeOffset endUtc)
    {
        if (timeProvider.GetUtcNow() >= endUtc)
        {
            throw new ApiException(
                ErrorCodes.BookingSessionEnded, 409,
                "This session has ended; you can no longer book a seat.",
                "انتهت هذه الجلسة، ولم يعد بإمكانك حجز مقعد.");
        }
    }

    /// <summary>The session's effective place count: the seat-layout total
    /// (rows × seatsPerRow) capped by the smaller of Session.CapacityOverride and
    /// Hall.Capacity. One definition shared by the reserve pre-check and the
    /// post-insert backstop so they can never disagree.</summary>
    private static int EffectiveCapacity(SessionContext ctx) =>
        Math.Min(
            ctx.RowLabels.Count * ctx.Layout!.SeatsPerRow,
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
        Guid actorUserId, DateTimeOffset now)
    {
        foreach (var rowLabel in ctx.RowLabels)
        {
            for (var seat = 1; seat <= ctx.Layout!.SeatsPerRow; seat++)
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
                    // M-6 — retained as the hold's outstanding-until marker.
                    ExpiresUtc = now + PendingHoldWindow,
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

    // P2.2 — D-227: fire the booking-confirmed in-app notification on APPROVE
    // (FDS-005 §5.2). The dispatcher writes to a different DbContext (Identity),
    // so the booking is already committed; a notification failure must never
    // fail or roll back the approval, hence the swallow-and-log.
    private async Task TryNotifyBookingConfirmedAsync(
        SeatReservation booking, (string Title, string TitleArabic) session,
        CancellationToken cancellationToken)
    {
        if (booking.ReservedForUserId is not { } userId)
        {
            return;
        }
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.BookingConfirmed,
                Title = "Seat reservation confirmed",
                TitleArabic = "تم تأكيد حجز المقعد",
                Body = booking.RowLabel is { } row
                    ? $"Your seat {row}{booking.SeatNumber} for \"{session.Title}\" is confirmed."
                    : $"Your place in \"{session.Title}\" is confirmed.",
                BodyArabic = booking.RowLabel is { } rowAr
                    ? $"تم تأكيد مقعدك {rowAr}{booking.SeatNumber} لجلسة \"{session.TitleArabic}\"."
                    : $"تم تأكيد حضورك لجلسة \"{session.TitleArabic}\".",
                Severity = NotificationSeverity.Success,
                RelatedEntityType = "Session",
                RelatedEntityId = booking.SessionId,
                SendEmail = false,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Booking-confirmed notification failed for reservation {ReservationId}",
                booking.Id);
        }
    }

    // P2.2 — D-227: tell the attendee their booking was rejected, with the
    // reason (FDS-005 §5.2). Same swallow-and-log discipline.
    private async Task TryNotifyBookingRejectedAsync(
        SeatReservation booking, (string Title, string TitleArabic) session,
        string reason, CancellationToken cancellationToken)
    {
        if (booking.ReservedForUserId is not { } userId)
        {
            return;
        }
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.BookingRejected,
                Title = "Seat booking rejected",
                TitleArabic = "تم رفض حجز المقعد",
                Body = booking.RowLabel is { } row
                    ? $"Your seat {row}{booking.SeatNumber} for \"{session.Title}\" was not approved. Reason: {reason}"
                    : $"Your booking for \"{session.Title}\" was not approved. Reason: {reason}",
                BodyArabic = booking.RowLabel is { } rowAr
                    ? $"لم تتم الموافقة على مقعدك {rowAr}{booking.SeatNumber} لجلسة \"{session.TitleArabic}\". السبب: {reason}"
                    : $"لم تتم الموافقة على حجزك لجلسة \"{session.TitleArabic}\". السبب: {reason}",
                Severity = NotificationSeverity.Warning,
                RelatedEntityType = "Session",
                RelatedEntityId = booking.SessionId,
                SendEmail = false,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Booking-rejected notification failed for reservation {ReservationId}",
                booking.Id);
        }
    }

    // M-4 — notify the attendee that an administrator released their seat
    // reservation (held or confirmed). Same swallow-and-log discipline as the
    // confirm/reject notifications: a notification failure never rolls back the
    // release (the dispatcher writes to the Identity DB, already committed here).
    private async Task TryNotifyBookingReleasedAsync(
        SeatReservation booking, (string Title, string TitleArabic) session,
        CancellationToken cancellationToken)
    {
        if (booking.ReservedForUserId is not { } userId)
        {
            return;
        }
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.BookingReleased,
                Title = "Seat reservation released",
                TitleArabic = "تم إلغاء حجز المقعد",
                Body = booking.RowLabel is { } row
                    ? $"Your seat {row}{booking.SeatNumber} for \"{session.Title}\" was released by the organiser."
                    : $"Your place in \"{session.Title}\" was released by the organiser.",
                BodyArabic = booking.RowLabel is { } rowAr
                    ? $"تم إلغاء مقعدك {rowAr}{booking.SeatNumber} لجلسة \"{session.TitleArabic}\" من قبل المنظّم."
                    : $"تم إلغاء حضورك لجلسة \"{session.TitleArabic}\" من قبل المنظّم.",
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
        DateTimeOffset StartUtc, DateTimeOffset EndUtc,
        SeatSelectionMode? SeatSelectionModeOverride);
    private sealed record SessionContext(
        Guid SessionId, Guid HallId, int? CapacityOverride, int HallCapacity,
        HallSeatLayout Layout, IReadOnlyList<string> RowLabels,
        string SessionTitle, string SessionTitleArabic,
        DateTimeOffset StartUtc, DateTimeOffset EndUtc,
        SeatSelectionMode EffectiveMode);
}
