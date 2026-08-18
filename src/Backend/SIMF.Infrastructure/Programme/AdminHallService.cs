// Tests: SIMF.Api.Tests/AdminHallCapacityTests.cs,
//        SIMF.Api.Tests/AdminHallGeofenceTests.cs,
//        SIMF.Api.Tests/ArrivalGraceResolutionTests.cs,
//        SIMF.Api.Tests/HallsExcelTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Common.Options;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>Admin CRUD over <see cref="Hall"/>. Mirrors
/// <see cref="AdminThemeService"/>; case-insensitive unique Code via
/// upper-case normalisation.</summary>
internal sealed class AdminHallService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminHallService> logger) : IAdminHallService
{
    /// <summary>
    /// The grid contract for /admin/halls: one entry per key HallsList.razor can
    /// send, as both its filter and its sort. A key not declared here is a 400, not
    /// a silently ignored request.
    /// </summary>
    private static readonly GridColumns<Hall> Columns = new GridColumns<Hall>()
        .Add("code", hall => hall.Code, searchable: true)
        .Add("name", hall => hall.Name, searchable: true)
        .Add("nameArabic", hall => hall.NameArabic, searchable: true)
        .Add("capacity", hall => hall.Capacity)
        .Add("floor", hall => hall.Floor)
        .Add("isActive", hall => hall.IsActive)
        .DefaultOrder("code")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<Hall, AdminHallSummary>> ToSummary =
        hall => new AdminHallSummary(
            hall.Id, hall.Code, hall.Name, hall.NameArabic,
            hall.Capacity, hall.Floor, hall.IsActive, hall.CreatedAt,
            (int)hall.Purpose,
            // Appended for the grid Excel round-trip (positional
            // order must match the AdminHallSummary record exactly).
            hall.FacilityNotes,
            hall.GeofenceCenterLat, hall.GeofenceCenterLon, hall.GeofenceRadiusMeters,
            (int)hall.SeatSelectionMode,
            hall.ArrivalGraceMinutes);

    public Task<GridPage<AdminHallSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.Halls.ToGridPageAsync(
            query, Columns, hall => hall.Id, ToSummary, cancellationToken);

    public async Task<AdminHallDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Halls.AsNoTracking()
            .Where(hall => hall.Id == id)
            .Select(hall => ToDetail(hall))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminHallDetail> CreateAsync(
        Guid actorUserId, AdminCreateHallRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, name, nameArabic, floor, facilityNotes) = Validate(request.Code,
            request.Name, request.NameArabic, request.Floor, request.FacilityNotes,
            request.Capacity);
        var (geoLat, geoLon, geoRadius) = ValidateGeofence(
            request.GeofenceCenterLat, request.GeofenceCenterLon, request.GeofenceRadiusMeters);
        var mode = ParseSeatSelectionMode(request.SeatSelectionMode);

        var clash = await dbContext.Halls.AsNoTracking()
            .AnyAsync(hall => hall.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(ErrorCodes.HallCodeDuplicate, 409,
                $"A hall with code '{code}' already exists.",
                $"توجد قاعة بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = code, Name = name, NameArabic = nameArabic,
            Capacity = request.Capacity,
            Floor = floor, FacilityNotes = facilityNotes,
            GeofenceCenterLat = geoLat, GeofenceCenterLon = geoLon, GeofenceRadiusMeters = geoRadius,
            SeatSelectionMode = mode,
            ArrivalGraceMinutes = ValidateArrivalGrace(request.ArrivalGraceMinutes),
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.Halls.Add(hall);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallCreated,
            actorUserId,
            $"id={hall.Id}; code={code}; name={name}",
            cancellationToken);

        logger.LogInformation("Admin {ActorId} created Hall {Code} ({Id})",
            actorUserId, code, hall.Id);

        return ToDetail(hall);
    }

    public async Task<AdminHallDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateHallRequest request,
        CancellationToken cancellationToken = default)
    {
        var hall = await dbContext.Halls
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall was not found.",
                "لم يتم العثور على القاعة.");

        var (code, name, nameArabic, floor, facilityNotes) = Validate(request.Code,
            request.Name, request.NameArabic, request.Floor, request.FacilityNotes,
            request.Capacity);
        var (geoLat, geoLon, geoRadius) = ValidateGeofence(
            request.GeofenceCenterLat, request.GeofenceCenterLon, request.GeofenceRadiusMeters);

        if (!string.Equals(hall.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.Halls.AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.HallCodeDuplicate, 409,
                    $"A hall with code '{code}' already exists.",
                    $"توجد قاعة بالرمز '{code}' بالفعل.");
            }
        }

        // A Capacity reduction must not drop below what the hall already
        // commits: its seat-layout total (rows × seats — SetLayoutAsync keeps this
        // ≤ Capacity) or the largest active (held) reservation count on any single
        // session held in this hall. Otherwise a shrink silently over-commits seats.
        if (request.Capacity < hall.Capacity)
        {
            await EnsureCapacityNotBelowUsageAsync(id, request.Capacity, cancellationToken);
        }

        // A37 — deactivating via the edit form's Active checkbox is the same
        // destructive act as the Deactivate action, so it takes the same guard.
        if (hall.IsActive && !request.IsActive)
        {
            await EnsureNoActiveSessionsAsync(id, cancellationToken);
        }

        hall.Code = code; hall.Name = name; hall.NameArabic = nameArabic;
        hall.Capacity = request.Capacity;
        hall.Floor = floor; hall.FacilityNotes = facilityNotes;
        hall.GeofenceCenterLat = geoLat; hall.GeofenceCenterLon = geoLon;
        hall.GeofenceRadiusMeters = geoRadius;
        hall.SeatSelectionMode = ParseSeatSelectionMode(request.SeatSelectionMode);
        hall.ArrivalGraceMinutes = ValidateArrivalGrace(request.ArrivalGraceMinutes);
        hall.IsActive = request.IsActive;
        hall.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallUpdated,
            actorUserId,
            $"id={hall.Id}; code={code}; active={hall.IsActive}",
            cancellationToken);

        return ToDetail(hall);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var hall = await dbContext.Halls
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall was not found.",
                "لم يتم العثور على القاعة.");

        if (!hall.IsActive) return;

        await EnsureNoActiveSessionsAsync(id, cancellationToken);

        hall.IsActive = false;
        hall.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallDeactivated,
            actorUserId,
            $"id={hall.Id}; code={hall.Code}",
            cancellationToken);
    }

    private static (string code, string name, string nameArabic, string? floor, string? facilityNotes)
        Validate(string codeRaw, string nameRaw, string nameArabicRaw,
                 string? floorRaw, string? notesRaw, int capacity)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Hall code must be between 2 and 16 characters.",
                "يجب أن يتراوح رمز القاعة بين 2 و 16 حرفاً.");
        }
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Hall English name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للقاعة بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Hall Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم العربي للقاعة بين 1 و 128 حرفاً.");
        }
        if (capacity < 0)
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Capacity must be zero or a positive integer.",
                "يجب أن تكون السعة صفراً أو عدداً صحيحاً موجباً.");
        }
        var floor = string.IsNullOrWhiteSpace(floorRaw) ? null : floorRaw.Trim();
        if (floor is { Length: > 32 })
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Floor must be 32 characters or fewer.",
                "يجب أن يكون عدد أحرف الطابق 32 حرفاً أو أقل.");
        }
        var notes = string.IsNullOrWhiteSpace(notesRaw) ? null : notesRaw.Trim();
        if (notes is { Length: > 1024 })
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Equipment notes must be 1024 characters or fewer.",
                "يجب أن تكون ملاحظات المعدات 1024 حرفاً أو أقل.");
        }
        return (code, name, nameArabic, floor, notes);
    }

    private static AdminHallDetail ToDetail(Hall hall) =>
        new(hall.Id, hall.Code, hall.Name, hall.NameArabic,
            hall.Capacity, hall.Floor, hall.FacilityNotes,
            hall.IsActive, hall.CreatedAt, hall.UpdatedAt,
            hall.GeofenceCenterLat, hall.GeofenceCenterLon, hall.GeofenceRadiusMeters,
            (int)hall.SeatSelectionMode,
            hall.ArrivalGraceMinutes);

    /// <summary>The arrival grace is optional (null = inherit the global
    /// value) but, when given, must be inside the one shared bound. Rejected rather
    /// than clamped: silently storing 240 when an admin typed 2400 would tell them
    /// the doors are open four times longer than they are.</summary>
    private static int? ValidateArrivalGrace(int? minutes)
    {
        if (!WalkInModeOptions.IsValidArrivalGrace(minutes))
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                $"Arrival grace must be between 0 and {WalkInModeOptions.MaxArrivalGraceMinutes} minutes.",
                $"يجب أن تكون مهلة الوصول بين 0 و{WalkInModeOptions.MaxArrivalGraceMinutes} دقيقة.");
        }
        return minutes;
    }

    /// <summary>Validate the incoming seat-selection mode int against the
    /// defined <see cref="SeatSelectionMode"/> values (the CP dropdown only offers
    /// 0/1, but a hand-crafted request must not store an undefined value).</summary>
    private static SeatSelectionMode ParseSeatSelectionMode(int raw)
    {
        if (!Enum.IsDefined(typeof(SeatSelectionMode), raw))
        {
            throw new ApiException(ErrorCodes.HallInvalid, 400,
                "Invalid seat-selection mode.",
                "وضع اختيار المقاعد غير صالح.");
        }
        return (SeatSelectionMode)raw;
    }

    /// <summary>Validate the optional hall geofence.
    /// All three values are set together or all null (a partial geofence is
    /// meaningless). When set: latitude −90..90, longitude −180..180, radius in
    /// metres &gt; 0 and ≤ 100 km (a venue-scale sanity cap).</summary>
    private static (double? Lat, double? Lon, double? Radius) ValidateGeofence(
        double? lat, double? lon, double? radius)
    {
        if (lat is null && lon is null && radius is null)
        {
            return (null, null, null);
        }
        if (lat is null || lon is null || radius is null)
        {
            throw new ApiException(ErrorCodes.HallGeofenceInvalid, 400,
                "The geofence needs a centre latitude, a centre longitude, and a radius — set all three or leave all empty.",
                "يحتاج السياج الجغرافي إلى خط عرض وخط طول للمركز ونصف قطر — اضبط الثلاثة معاً أو اتركها فارغة.");
        }
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            throw new ApiException(ErrorCodes.HallGeofenceInvalid, 400,
                "The geofence centre must be a valid coordinate (latitude −90..90, longitude −180..180).",
                "يجب أن يكون مركز السياج إحداثية صحيحة (خط العرض −90..90، خط الطول −180..180).");
        }
        if (radius is <= 0 or > 100_000)
        {
            throw new ApiException(ErrorCodes.HallGeofenceInvalid, 400,
                "The geofence radius must be greater than 0 and at most 100000 metres.",
                "يجب أن يكون نصف قطر السياج أكبر من 0 ولا يتجاوز 100000 متر.");
        }
        return (lat, lon, radius);
    }

    /// <summary>A37 — refuses to deactivate a hall that active sessions still sit in.
    /// The flip itself always "worked"; the breakage surfaced later and somewhere else,
    /// as a 400 <c>SESSION_HALL_NOT_FOUND</c> the next time anyone edited one of those
    /// sessions (<c>AdminSessionService.ResolveHallAsync</c> rejects an inactive hall).
    /// The bilingual message names the count so the admin knows how much re-homing the
    /// change costs. Throws <see cref="ErrorCodes.HallInUse"/> — the code the halls
    /// docs already reserved for exactly this guard.</summary>
    private async Task EnsureNoActiveSessionsAsync(
        Guid hallId, CancellationToken cancellationToken)
    {
        var sessionsInHall = await dbContext.Sessions
            .AsNoTracking()
            .CountAsync(session => session.HallId == hallId && session.IsActive, cancellationToken);
        if (sessionsInHall > 0)
        {
            throw new ApiException(ErrorCodes.HallInUse, 409,
                $"This hall is still used by {sessionsInHall} active session(s) — move or deactivate them before deactivating the hall.",
                $"لا تزال هذه القاعة مستخدمة في {sessionsInHall} جلسة نشطة — انقلها أو ألغِ تفعيلها قبل إلغاء تفعيل القاعة.");
        }
    }

    /// <summary>Guards a hall Capacity reduction. The new capacity must be
    /// ≥ the hall's seat-layout total AND ≥ the largest active
    /// (held, not-released) reservation count on any single session held in this
    /// hall. Throws <see cref="ErrorCodes.HallCapacityBelowUsage"/> otherwise.
    /// RowLabels and SeatCounts are comma-separated CSVs (the same format
    /// ParseRowLabels / ExpandSeatCounts read) so the layout total is computed in
    /// memory after projecting the columns; all queries stay within
    /// SimfAppDbContext (no cross-DB access).</summary>
    private async Task EnsureCapacityNotBelowUsageAsync(
        Guid hallId, int newCapacity, CancellationToken cancellationToken)
    {
        var layout = await dbContext.HallSeatLayouts.AsNoTracking()
            .Where(seatLayout => seatLayout.HallId == hallId)
            .Select(seatLayout => new
            {
                seatLayout.RowLabels, seatLayout.SeatsPerRow, seatLayout.SeatCounts,
            })
            .SingleOrDefaultAsync(cancellationToken);
        var layoutTotal = layout is null
            ? 0
            : SeatLayoutTotal(layout.RowLabels, layout.SeatsPerRow, layout.SeatCounts);

        var maxActivePerSession = await dbContext.SeatReservations.AsNoTracking()
            .Where(reservation => reservation.ReleasedAt == null)
            .Join(
                dbContext.Sessions.AsNoTracking()
                    .Where(session => session.HallId == hallId),
                reservation => reservation.SessionId,
                session => session.Id,
                (reservation, session) => reservation.SessionId)
            .GroupBy(sessionId => sessionId)
            .Select(perSession => perSession.Count())
            .OrderByDescending(count => count)
            .FirstOrDefaultAsync(cancellationToken);

        var committed = Math.Max(layoutTotal, maxActivePerSession);
        if (newCapacity < committed)
        {
            throw new ApiException(ErrorCodes.HallCapacityBelowUsage, 409,
                $"Capacity cannot drop below what this hall already commits ({committed}).",
                $"لا يمكن خفض السعة دون ما تلتزم به هذه القاعة بالفعل ({committed}).");
        }
    }

    /// <summary>The seat total a layout actually commits. SeatsPerRow is only the
    /// uniform row width while SeatCounts is null; once the grid is ragged it holds
    /// max(SeatCounts), so rows × SeatsPerRow overstates the total and rejected
    /// capacity reductions that were legitimate: rows of 20, 5, 5 really commit 30
    /// seats, not 60.
    /// <para>A SeatCounts CSV that does not parse, or whose length no longer matches
    /// the row set, falls back to the uniform product. That is the LARGER of the two
    /// figures, so corrupt layout state can only ever make this guard stricter,
    /// never let a reduction through that the seat grid cannot honour. Reporting the
    /// corruption belongs to the seat paths that own the column
    /// (SeatReservationService.ExpandSeatCounts raises a deterministic 500 there);
    /// a hall rename is the wrong place to surface it.</para></summary>
    private static int SeatLayoutTotal(string? rowLabels, int seatsPerRow, string? seatCounts)
    {
        var rowCount = (rowLabels ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
        var uniformTotal = rowCount * seatsPerRow;
        if (string.IsNullOrWhiteSpace(seatCounts))
        {
            return uniformTotal;
        }

        var parts = seatCounts.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != rowCount)
        {
            return uniformTotal;
        }

        var raggedTotal = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var seatsInRow))
            {
                return uniformTotal;
            }
            raggedTotal += seatsInRow;
        }
        return raggedTotal;
    }
}
