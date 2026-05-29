// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
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
    IAuditLog auditLog,
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
            cells, mine, cells.Count);
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
        };
        await PersistWithUniquenessGuardAsync(reservation, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"reservationId={reservation.Id}; sessionId={sessionId}; "
                + $"row={row}; seat={seat}; kind=UserBooking",
        }, cancellationToken);

        logger.LogInformation(
            "Seat {Row}{Seat} reserved on session {SessionId} by user {Actor}",
            row, seat, sessionId, actorUserId);

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
                        + $"row={rowLabel}; seat={seat}; kind=RandomAssignment",
                }, cancellationToken);
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
        mine.ReleasedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SeatReservationReleased,
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

    // -- internals --

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
            hallCapacity, layout, ParseRowLabels(layout.RowLabels));
    }

    private async Task<SessionSnapshot> LoadSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        return await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new SessionSnapshot(s.Id, s.HallId, s.CapacityOverride))
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

    private static MySeatReservation ToMine(SeatReservation r) =>
        new(r.Id, r.SessionId, r.RowLabel, r.SeatNumber, r.Kind, r.CreatedAt);

    private sealed record SessionSnapshot(Guid Id, Guid HallId, int? CapacityOverride);
    private sealed record SessionContext(
        Guid SessionId, Guid HallId, int? CapacityOverride, int HallCapacity,
        HallSeatLayout Layout, IReadOnlyList<string> RowLabels);
}
