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
            })
            .ToListAsync(cancellationToken);

        var cells = reservations.Select(r => new SessionSeatCell(
            r.Id, r.RowLabel, r.SeatNumber, r.Kind)).ToList();

        SessionSeatCell? mine = null;
        if (actorUserId is { } actor)
        {
            var ownRow = reservations.FirstOrDefault(r => r.ReservedForUserId == actor);
            if (ownRow is not null)
            {
                mine = new SessionSeatCell(
                    ownRow.Id, ownRow.RowLabel, ownRow.SeatNumber, ownRow.Kind);
            }
        }

        var hallCapacity = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => h.Capacity)
            .SingleAsync(cancellationToken);

        return new SessionSeatMap(
            sessionId, session.HallId, hallCapacity, session.CapacityOverride,
            rowLabels, layout?.SeatsPerRow ?? 0,
            cells, mine, cells.Count,
            // D-432 — the session title is already loaded in the snapshot.
            session.Title, session.TitleArabic);
    }

    public async Task<MySeatReservation> ReserveAsync(
        Guid sessionId, Guid actorUserId,
        ReserveSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = (request.RowLabel ?? string.Empty).Trim();
        var seat = request.SeatNumber;
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
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

        await EnsureNoOverlapAsync(sessionId, actorUserId, ctx, cancellationToken);

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
            // P2.2 — D-227: the seat is HELD but awaits Control Panel approval.
            Status = BookingStatus.Pending,
        };
        await PersistWithUniquenessGuardAsync(reservation, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={row}; seat={seat}; kind=UserBooking; status=Pending",
        }, cancellationToken);

        logger.LogInformation(
            "Seat {Row}{Seat} booked (pending approval) on session {SessionId} by user {Actor}",
            row, seat, sessionId, actorUserId);

        // P2.2 — D-227: booking-confirmed now fires on APPROVE, not reserve
        // (FDS-005 §5.2). A fresh booking is Pending; nothing is dispatched here.
        return ToMine(reservation);
    }

    public async Task<MySeatReservation> ReserveRandomAsync(
        Guid sessionId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await BuildContextAsync(sessionId, cancellationToken);
        await EnsureSessionHasCapacityAsync(ctx, cancellationToken);

        var existing = await GetMyActiveAsync(sessionId, actorUserId, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException(
                ErrorCodes.SeatAlreadyOwnedBySession, 409,
                "You already have a seat reserved for this session.",
                "لديك مقعد محجوز بالفعل لهذه الجلسة.");
        }

        await EnsureNoOverlapAsync(sessionId, actorUserId, ctx, cancellationToken);

        var occupied = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null)
            .Select(r => new { r.RowLabel, r.SeatNumber })
            .ToListAsync(cancellationToken);
        var taken = new HashSet<(string Row, int Seat)>(
            occupied.Select(o => (o.RowLabel, o.SeatNumber)));

        foreach (var rowLabel in ctx.RowLabels)
        {
            for (var seat = 1; seat <= ctx.Layout!.SeatsPerRow; seat++)
            {
                if (taken.Contains((rowLabel, seat))) continue;
                var reservation = new SeatReservation
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    RowLabel = rowLabel,
                    SeatNumber = seat,
                    Kind = SeatReservationKind.RandomAssignment,
                    ReservedForUserId = actorUserId,
                    CreatedByUserId = actorUserId,
                    CreatedAt = timeProvider.GetUtcNow(),
                    // P2.2 — D-227: held, pending Control Panel approval.
                    Status = BookingStatus.Pending,
                };
                try
                {
                    await PersistWithUniquenessGuardAsync(reservation, cancellationToken);
                }
                catch (ApiException ex) when (ex.Code == ErrorCodes.SeatAlreadyReserved)
                {
                    taken.Add((rowLabel, seat));
                    continue;
                }

                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.SeatReservationCreated,
                    Outcome = AuditOutcome.Success,
                    ActorUserId = actorUserId,
                    Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                        + $"row={rowLabel}; seat={seat}; kind=RandomAssignment; status=Pending",
                }, cancellationToken);

                // P2.2 — D-227: booking-confirmed fires on approve, not here.
                return ToMine(reservation);
            }
        }

        throw new ApiException(
            ErrorCodes.SeatSessionFull, 409,
            "No seats remain in this session.",
            "لا توجد مقاعد متبقية في هذه الجلسة.");
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
        var taken = new HashSet<int>(occupiedInRow);

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
        reservation.ReleasedAt = timeProvider.GetUtcNow();
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
    }

    public async Task<GridPage<SessionSeatCell>> ListSessionReservationsAsync(
        Guid sessionId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 500);

        var baseQuery = appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ReleasedAt == null);
        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderBy(r => r.RowLabel).ThenBy(r => r.SeatNumber)
            .Skip(skip).Take(top)
            .Select(r => new SessionSeatCell(r.Id, r.RowLabel, r.SeatNumber, r.Kind))
            .ToListAsync(cancellationToken);
        return GridPage<SessionSeatCell>.Of(rows, total,
            new GridQuery { Skip = skip, Top = top });
    }

    // -- Booking approval queue (P2.2 / D-227 — FDS-005 §5.2) --

    public async Task<GridPage<BookingQueueRow>> ListPendingBookingsAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 500);

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
                    joined = joined.Where(x => x.RowLabel.Contains(v));
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
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task ApproveBookingAsync(
        Guid actorUserId, Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadPendingBookingAsync(reservationId, cancellationToken);

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

    private Task<(string Title, string TitleArabic)> LoadSessionTitleAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new ValueTuple<string, string>(s.Title, s.TitleArabic))
            .SingleAsync(cancellationToken);

    private async Task EnsureNoOverlapAsync(
        Guid sessionId, Guid actorUserId, SessionContext ctx,
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
            .AnyAsync(x => x.StartUtc < ctx.EndUtc && ctx.StartUtc < x.EndUtc,
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
        var hallCapacity = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == session.HallId)
            .Select(h => h.Capacity)
            .SingleAsync(cancellationToken);
        return new SessionContext(
            session.Id, session.HallId, session.CapacityOverride,
            hallCapacity, layout, ParseRowLabels(layout.RowLabels),
            session.Title, session.TitleArabic, session.StartUtc, session.EndUtc);
    }

    private async Task<SessionSnapshot> LoadSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        return await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new SessionSnapshot(
                s.Id, s.HallId, s.CapacityOverride, s.Title, s.TitleArabic,
                s.StartUtc, s.EndUtc))
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

    private static IReadOnlyList<string> ParseRowLabels(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

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
        var layoutCap = ctx.RowLabels.Count * ctx.Layout!.SeatsPerRow;
        var declaredCap = ctx.CapacityOverride ?? ctx.HallCapacity;
        var effective = Math.Min(layoutCap, declaredCap);
        var active = await appDbContext.SeatReservations
            .Where(r => r.SessionId == ctx.SessionId && r.ReleasedAt == null)
            .CountAsync(cancellationToken);
        if (active >= effective)
        {
            throw new ApiException(
                ErrorCodes.SeatSessionFull, 409,
                "No seats remain in this session.",
                "لا توجد مقاعد متبقية في هذه الجلسة.");
        }
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
                Body = $"Your seat {booking.RowLabel}{booking.SeatNumber} "
                    + $"for \"{session.Title}\" is confirmed.",
                BodyArabic = $"تم تأكيد مقعدك {booking.RowLabel}{booking.SeatNumber} "
                    + $"لجلسة \"{session.TitleArabic}\".",
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
                Body = $"Your seat {booking.RowLabel}{booking.SeatNumber} for "
                    + $"\"{session.Title}\" was not approved. Reason: {reason}",
                BodyArabic = $"لم تتم الموافقة على مقعدك {booking.RowLabel}{booking.SeatNumber} "
                    + $"لجلسة \"{session.TitleArabic}\". السبب: {reason}",
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

    private static MySeatReservation ToMine(SeatReservation r) =>
        new(r.Id, r.SessionId, r.RowLabel, r.SeatNumber, r.Kind, r.CreatedAt, r.Status);

    private sealed record SessionSnapshot(
        Guid Id, Guid HallId, int? CapacityOverride, string Title, string TitleArabic,
        DateTimeOffset StartUtc, DateTimeOffset EndUtc);
    private sealed record SessionContext(
        Guid SessionId, Guid HallId, int? CapacityOverride, int HallCapacity,
        HallSeatLayout Layout, IReadOnlyList<string> RowLabels,
        string SessionTitle, string SessionTitleArabic,
        DateTimeOffset StartUtc, DateTimeOffset EndUtc);
}
