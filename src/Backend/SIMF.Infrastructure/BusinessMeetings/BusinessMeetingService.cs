// Tests: SIMF.Api.Tests/BusinessMeetingsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.BusinessMeetings.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Domain.BusinessMeetings;
using SIMF.Infrastructure.MeetingRequests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.BusinessMeetings;

/// <summary>SIMF-FDS-013 — D-248: flexible hall configuration + admin-arranged
/// B2B/B2C business meetings. Halls carry a <see cref="HallPurpose"/>; Meeting /
/// General halls hold <see cref="MeetingTable"/>s (added one-by-one or generated
/// random-by-count / by row-column); <see cref="HallAllocation"/> reserves hall
/// space over a from–to slot; a <see cref="BusinessMeeting"/> schedules two or more
/// parties (companies + visitors) at a table. Visitor names resolve via a second
/// Identity round-trip (no cross-DB JOIN, D-157); company refs are App FKs.</summary>
internal sealed class BusinessMeetingService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    INotificationDispatcher notifications,
    IForumWindowService forumWindow,
    TimeProvider timeProvider,
    ILogger<BusinessMeetingService> logger) : IBusinessMeetingService
{
    private const int MaxParticipants = 50;

    /// <summary>The event's local-day boundary (KSA, UTC+3) — the same convention
    /// the programme uses to bucket a session to a Riyadh calendar day. A meeting's
    /// start/end are converted to this zone before the forum-day bound is checked so
    /// a late-evening UTC slot files under the correct event day.</summary>
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    /// <summary>Hard ceiling on the number of active meeting tables a single hall
    /// may hold — bounds a generate call regardless of the hall's configured
    /// capacity (which may be 0), so a huge or unbounded request can never
    /// materialise a runaway batch.</summary>
    private const int MaxTablesPerHall = 500;

    // ── Hall purpose ─────────────────────────────────────────────────────────

    public async Task SetHallPurposeAsync(
        Guid actorUserId, Guid hallId, SetHallPurposeRequest request,
        CancellationToken cancellationToken = default)
    {
        var hall = await appDbContext.Halls
            .SingleOrDefaultAsync(h => h.Id == hallId && h.IsActive, cancellationToken)
            ?? throw NotFound(ErrorCodes.HallNotFound, "Hall not found.", "لم يتم العثور على القاعة.");

        hall.Purpose = request.Purpose;
        hall.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallPurposeChanged,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"hallId={hallId}; purpose={request.Purpose}",
        }, cancellationToken);
    }

    // ── Meeting tables ───────────────────────────────────────────────────────

    public async Task<GridPage<MeetingTableRow>> ListTablesAsync(
        Guid hallId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = Page(query);
        var baseQuery = appDbContext.MeetingTables.AsNoTracking()
            .Where(t => t.HallId == hallId && t.IsActive);
        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderBy(t => t.Code)
            .Skip(skip).Take(top)
            .Select(t => new MeetingTableRow(
                t.Id, t.HallId, t.Code, t.RowLabel, t.ColumnNumber, t.Capacity, t.IsActive))
            .ToListAsync(cancellationToken);
        return GridPage<MeetingTableRow>.Of(rows, total, skip, top);
    }

    public async Task<MeetingTableRow> CreateTableAsync(
        Guid actorUserId, Guid hallId, CreateMeetingTableRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureMeetingHallAsync(hallId, cancellationToken);
        var code = NormaliseCode(request.Code);
        ValidateCapacity(request.Capacity);
        await EnsureCodeFreeAsync(hallId, code, cancellationToken);

        var table = new MeetingTable
        {
            Id = Guid.NewGuid(),
            HallId = hallId,
            Code = code,
            RowLabel = Trim(request.RowLabel),
            ColumnNumber = request.ColumnNumber,
            Capacity = request.Capacity,
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        appDbContext.MeetingTables.Add(table);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingTableCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"tableId={table.Id}; hallId={hallId}; code={code}; capacity={request.Capacity}",
        }, cancellationToken);

        return ToTableRow(table);
    }

    public async Task<MeetingTableRow> UpdateTableAsync(
        Guid actorUserId, Guid tableId, UpdateMeetingTableRequest request,
        CancellationToken cancellationToken = default)
    {
        var table = await appDbContext.MeetingTables
            .SingleOrDefaultAsync(t => t.Id == tableId && t.IsActive, cancellationToken)
            ?? throw NotFound(ErrorCodes.MeetingTableNotFound,
                "Meeting table not found.", "لم يتم العثور على طاولة الاجتماع.");

        var code = NormaliseCode(request.Code);
        ValidateCapacity(request.Capacity);
        if (!string.Equals(code, table.Code, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCodeFreeAsync(table.HallId, code, cancellationToken);
        }

        table.Code = code;
        table.RowLabel = Trim(request.RowLabel);
        table.ColumnNumber = request.ColumnNumber;
        table.Capacity = request.Capacity;
        table.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingTableUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"tableId={tableId}; code={code}; capacity={request.Capacity}",
        }, cancellationToken);

        return ToTableRow(table);
    }

    public async Task DeleteTableAsync(
        Guid actorUserId, Guid tableId, CancellationToken cancellationToken = default)
    {
        var table = await appDbContext.MeetingTables
            .SingleOrDefaultAsync(t => t.Id == tableId && t.IsActive, cancellationToken)
            ?? throw NotFound(ErrorCodes.MeetingTableNotFound,
                "Meeting table not found.", "لم يتم العثور على طاولة الاجتماع.");

        var now = timeProvider.SimfNow();
        var hasScheduled = await appDbContext.BusinessMeetings.AsNoTracking()
            .AnyAsync(m => m.MeetingTableId == tableId
                && m.Status == BusinessMeetingStatus.Confirmed
                && m.End > now, cancellationToken);
        if (hasScheduled)
        {
            throw new ApiException(
                ErrorCodes.MeetingTableInvalid, 409,
                "This table has upcoming meetings; cancel them first.",
                "هذه الطاولة لديها اجتماعات قادمة؛ يرجى إلغاؤها أولاً.");
        }

        table.Deactivate();
        table.UpdatedAt = now;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingTableDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"tableId={tableId}; hallId={table.HallId}",
        }, cancellationToken);
    }

    public async Task<MeetingTablesGenerated> GenerateTablesAsync(
        Guid actorUserId, Guid hallId, GenerateMeetingTablesRequest request,
        CancellationToken cancellationToken = default)
    {
        var hall = await EnsureMeetingHallAsync(hallId, cancellationToken);
        ValidateCapacity(request.Capacity);
        var now = timeProvider.SimfNow();

        var existing = await appDbContext.MeetingTables
            .Where(t => t.HallId == hallId && t.IsActive)
            .ToListAsync(cancellationToken);

        var removed = 0;
        if (request.Reset)
        {
            foreach (var t in existing)
            {
                t.Deactivate();
                t.UpdatedAt = now;
            }
            removed = existing.Count;
            existing = [];
        }

        var takenCodes = new HashSet<string>(
            existing.Select(t => t.Code), StringComparer.OrdinalIgnoreCase);
        var toCreate = new List<MeetingTable>();

        // Total active tables in a hall are bounded by the hall's capacity (the
        // owner's "stop at max") and a hard MaxTablesPerHall ceiling, so a
        // 0-capacity hall or a huge requested count can never run away. The free
        // slots subtract the tables already present (after any Reset).
        var hardCeiling = hall.Capacity > 0
            ? Math.Min(hall.Capacity, MaxTablesPerHall)
            : MaxTablesPerHall;
        var freeSlots = Math.Max(0, hardCeiling - existing.Count);

        if (request.Mode == HallAllocationMode.RandomByCount)
        {
            if (request.Count is not > 0)
            {
                throw Invalid(ErrorCodes.HallAllocationInvalid,
                    "A positive table count is required.",
                    "يلزم إدخال عدد طاولات موجب.");
            }
            // Create up to the requested count, bounded by the hall's free table slots.
            var target = Math.Min(request.Count.Value, freeSlots);
            var seq = 1;
            for (var i = 0; i < target; i++)
            {
                string code;
                do { code = $"T-{seq:000}"; seq++; }
                while (!takenCodes.Add(code));
                toCreate.Add(NewTable(hallId, code, null, null, request.Capacity, now));
            }
        }
        else if (request.Mode == HallAllocationMode.RowColumn)
        {
            var tokens = ParseCsv(request.RowColumnSpec);
            if (tokens.Count == 0)
            {
                throw Invalid(ErrorCodes.HallAllocationInvalid,
                    "Provide a row/column spec, e.g. \"A1,A2,B3\".",
                    "يرجى إدخال مخطط صف/عمود، مثل \"A1,A2,B3\".");
            }
            if (tokens.Count > freeSlots)
            {
                throw Invalid(ErrorCodes.HallAllocationInvalid,
                    $"That would exceed the hall's table capacity ({hardCeiling}).",
                    $"سيتجاوز ذلك سعة طاولات القاعة ({hardCeiling}).");
            }
            foreach (var token in tokens)
            {
                var code = NormaliseCode(token);
                if (!takenCodes.Add(code)) continue; // skip duplicates
                var (row, col) = SplitRowColumn(code);
                toCreate.Add(NewTable(hallId, code, row, col, request.Capacity, now));
            }
        }
        else
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "Whole-hall reservation is not a table-generation mode.",
                "حجز القاعة بالكامل ليس وضع توليد طاولات.");
        }

        appDbContext.MeetingTables.AddRange(toCreate);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingTablesGenerated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"hallId={hallId}; mode={request.Mode}; created={toCreate.Count}; "
                + $"removed={removed}; reset={request.Reset}",
        }, cancellationToken);

        return new MeetingTablesGenerated(toCreate.Count, removed);
    }

    // ── Hall allocations ─────────────────────────────────────────────────────

    public async Task<GridPage<HallAllocationRow>> ListAllocationsAsync(
        Guid hallId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = Page(query);
        var baseQuery = appDbContext.HallAllocations.AsNoTracking()
            .Where(a => a.HallId == hallId && a.ReleasedAt == null);
        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderByDescending(a => a.Start)
            .Skip(skip).Take(top)
            .Select(a => new HallAllocationRow(
                a.Id, a.HallId, a.Purpose, a.Mode, a.UnitCount, a.RowColumnSpec,
                a.Start, a.End, a.Notes))
            .ToListAsync(cancellationToken);
        return GridPage<HallAllocationRow>.Of(rows, total, skip, top);
    }

    public async Task<HallAllocationRow> CreateAllocationAsync(
        Guid actorUserId, Guid hallId, CreateHallAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var hall = await appDbContext.Halls.AsNoTracking()
            .SingleOrDefaultAsync(h => h.Id == hallId && h.IsActive, cancellationToken)
            ?? throw NotFound(ErrorCodes.HallNotFound, "Hall not found.", "لم يتم العثور على القاعة.");

        await ValidateSlotAsync(request.Start, request.End, cancellationToken);
        if (request.Mode == HallAllocationMode.RandomByCount && request.UnitCount is not > 0)
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "A positive unit count is required for random allocation.",
                "يلزم إدخال عدد وحدات موجب للتخصيص العشوائي.");
        }
        if (request.Mode == HallAllocationMode.RowColumn && ParseCsv(request.RowColumnSpec).Count == 0)
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "A row/column spec is required for row/column allocation.",
                "يلزم إدخال مخطط صف/عمود لتخصيص الصف/العمود.");
        }

        // A hall slot cannot be double-reserved (session vs meeting, etc.): reject
        // any active allocation in the hall whose window overlaps this one.
        var overlaps = await appDbContext.HallAllocations.AsNoTracking()
            .Where(a => a.HallId == hallId && a.ReleasedAt == null)
            .AnyAsync(a => a.Start < request.End && request.Start < a.End,
                cancellationToken);
        if (overlaps)
        {
            throw new ApiException(
                ErrorCodes.HallAllocationOverlap, 409,
                "The hall is already reserved for an overlapping time-slot.",
                "القاعة محجوزة بالفعل في فترة زمنية متداخلة.");
        }

        var allocation = new HallAllocation
        {
            Id = Guid.NewGuid(),
            HallId = hallId,
            Purpose = request.Purpose,
            Mode = request.Mode,
            UnitCount = request.Mode == HallAllocationMode.RandomByCount ? request.UnitCount : null,
            RowColumnSpec = request.Mode == HallAllocationMode.RowColumn
                ? Trim(request.RowColumnSpec) : null,
            Start = request.Start,
            End = request.End,
            CreatedByUserId = actorUserId,
            Notes = Trim(request.Notes),
            CreatedAt = timeProvider.SimfNow(),
        };
        appDbContext.HallAllocations.Add(allocation);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallAllocationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"allocationId={allocation.Id}; hallId={hallId}; purpose={request.Purpose}; "
                + $"mode={request.Mode}; from={request.Start:o}; to={request.End:o}",
        }, cancellationToken);

        return new HallAllocationRow(
            allocation.Id, allocation.HallId, allocation.Purpose, allocation.Mode,
            allocation.UnitCount, allocation.RowColumnSpec, allocation.Start,
            allocation.End, allocation.Notes);
    }

    public async Task ReleaseAllocationAsync(
        Guid actorUserId, Guid allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await appDbContext.HallAllocations
            .SingleOrDefaultAsync(a => a.Id == allocationId && a.ReleasedAt == null, cancellationToken)
            ?? throw NotFound(ErrorCodes.HallAllocationNotFound,
                "Hall allocation not found.", "لم يتم العثور على تخصيص القاعة.");

        allocation.ReleasedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallAllocationReleased,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"allocationId={allocationId}; hallId={allocation.HallId}",
        }, cancellationToken);
    }

    // ── Business meetings ────────────────────────────────────────────────────

    public async Task<GridPage<BusinessMeetingRow>> ListMeetingsAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = Page(query);

        // Filter + sort on the entity navigations first (all App-DB columns, so
        // EF-translatable), and project into BusinessMeetingRow LAST — after
        // Skip/Take. Ordering before projecting keeps the Participants.Count
        // correlated subquery out of the ORDER BY: EF Core cannot translate an
        // OrderBy whose key is a projection that embeds a subquery (both the old
        // manual double-Join and a post-projection sort 500'd at execution).
        var q = appDbContext.BusinessMeetings.AsNoTracking();

        // CP grid per-column filters (D-255). Unknown columns are ignored. The
        // legacy "status" filter (the old dropdown) still parses the enum name.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "hall":
                    q = q.Where(m => m.MeetingTable!.Hall!.NameArabic.Contains(v));
                    break;
                case "table":
                    q = q.Where(m => m.MeetingTable!.Code.Contains(v));
                    break;
                case "status":
                    if (Enum.TryParse<BusinessMeetingStatus>(v, ignoreCase: true, out var status))
                    {
                        q = q.Where(m => m.Status == status);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: most recent start first.
        q = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("hall", false) => q.OrderBy(m => m.MeetingTable!.Hall!.NameArabic).ThenByDescending(m => m.Start),
            ("hall", true) => q.OrderByDescending(m => m.MeetingTable!.Hall!.NameArabic).ThenByDescending(m => m.Start),
            ("table", false) => q.OrderBy(m => m.MeetingTable!.Code).ThenByDescending(m => m.Start),
            ("table", true) => q.OrderByDescending(m => m.MeetingTable!.Code).ThenByDescending(m => m.Start),
            ("type", false) => q.OrderBy(m => m.MeetingType).ThenByDescending(m => m.Start),
            ("type", true) => q.OrderByDescending(m => m.MeetingType).ThenByDescending(m => m.Start),
            ("start", false) => q.OrderBy(m => m.Start),
            ("end", false) => q.OrderBy(m => m.End),
            ("end", true) => q.OrderByDescending(m => m.End),
            ("status", false) => q.OrderBy(m => m.Status).ThenByDescending(m => m.Start),
            ("status", true) => q.OrderByDescending(m => m.Status).ThenByDescending(m => m.Start),
            _ => q.OrderByDescending(m => m.Start),
        };

        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip(skip).Take(top)
            .Select(m => new BusinessMeetingRow(
                m.Id, m.MeetingTableId, m.MeetingTable!.Code,
                m.MeetingTable.HallId, m.MeetingTable.Hall!.NameArabic,
                m.MeetingType, m.Start, m.End, m.Status,
                m.Participants.Count))
            .ToListAsync(cancellationToken);

        return GridPage<BusinessMeetingRow>.Of(items, total, skip, top);
    }

    public async Task<BusinessMeetingDetail> GetMeetingAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // One round-trip: the table + its hall are real navigations off the meeting.
        var meeting = await appDbContext.BusinessMeetings.AsNoTracking()
            .Include(m => m.Participants)
            .Include(m => m.MeetingTable!).ThenInclude(t => t.Hall!)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw NotFound(ErrorCodes.BusinessMeetingNotFound,
                "Meeting not found.", "لم يتم العثور على الاجتماع.");

        var table = meeting.MeetingTable!;
        var hall = table.Hall!;

        var participants = meeting.Participants
            .Select(p => new MeetingParticipantDto(
                p.Kind, p.ExhibitorId, p.VisitorUserId, p.DisplayNameSnapshot))
            .ToList();

        return new BusinessMeetingDetail(
            meeting.Id, meeting.MeetingTableId, table.Code, table.HallId, hall.NameArabic,
            meeting.MeetingType, meeting.Start, meeting.End, meeting.Status,
            meeting.Notes, meeting.CancellationReason, meeting.CreatedAt,
            meeting.CancelledAt, participants);
    }

    public async Task<BusinessMeetingScheduled> ScheduleMeetingAsync(
        Guid actorUserId, ScheduleMeetingRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateSlotAsync(request.Start, request.End, cancellationToken);

        var table = await appDbContext.MeetingTables.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == request.MeetingTableId && t.IsActive, cancellationToken)
            ?? throw NotFound(ErrorCodes.MeetingTableNotFound,
                "Meeting table not found.", "لم يتم العثور على طاولة الاجتماع.");
        await EnsureMeetingHallAsync(table.HallId, cancellationToken);

        var parties = NormaliseParticipants(request.Participants);
        if (parties.Count < 2)
        {
            throw Invalid(ErrorCodes.MeetingParticipantInvalid,
                "A meeting needs at least two distinct participants.",
                "يحتاج الاجتماع إلى مشاركَين مختلفين على الأقل.");
        }
        if (parties.Count > table.Capacity)
        {
            throw new ApiException(
                ErrorCodes.MeetingCapacityExceeded, 409,
                $"The table seats {table.Capacity}; {parties.Count} participants were given.",
                $"تتسع الطاولة لـ {table.Capacity}؛ تم إدخال {parties.Count} مشاركاً.");
        }

        var companyIds = parties.Where(p => p.Kind == MeetingPartyKind.Company)
            .Select(p => p.CompanyId!.Value).ToList();
        var visitorIds = parties.Where(p => p.Kind == MeetingPartyKind.Visitor)
            .Select(p => p.VisitorUserId!.Value).ToList();

        var names = await ResolvePartyNamesAsync(companyIds, visitorIds, cancellationToken);

        var now = timeProvider.SimfNow();
        var meeting = new BusinessMeeting
        {
            Id = Guid.NewGuid(),
            MeetingTableId = table.Id,
            MeetingType = request.MeetingType,
            Start = request.Start,
            End = request.End,
            Status = BusinessMeetingStatus.Confirmed,
            Notes = Trim(request.Notes),
            ScheduledByUserId = actorUserId,
            CreatedAt = now,
            Participants = parties.Select(p => new BusinessMeetingParticipant
            {
                Id = Guid.NewGuid(),
                Kind = p.Kind,
                ExhibitorId = p.CompanyId,
                VisitorUserId = p.VisitorUserId,
                DisplayNameSnapshot = names.TryGetValue(p.Key, out var n) ? n : string.Empty,
                CreatedAt = now,
            }).ToList(),
        };

        // M-5 — close the read-then-insert double-book race. A time range cannot be a
        // SQL unique constraint, so the table / hall / participant overlap checks and
        // the insert must run inside ONE Serializable transaction: the range scans then
        // hold key-range locks and a concurrent overlapping insert cannot slip in
        // between a check and the save. Run through the EF execution strategy so it
        // composes with EnableRetryOnFailure (a user transaction otherwise throws under
        // the retrying strategy); on a serialization/deadlock failure the strategy
        // re-runs the whole unit and the re-checks see the now-committed rival and raise
        // the clean 409. The meeting is built once above (fixed Id) so a retry re-inserts
        // the same row instead of duplicating it.
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await appDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            // Table conflict — the table is already held over this slot. D-773: the
            // scan used to see this service's OWN family only, so a business meeting
            // could be scheduled onto a table already held by a delegation or speaker
            // meeting request. The shared guard covers all three families; running it
            // here keeps its range scans inside this Serializable transaction, so the
            // key-range locks that close the M-5 race still cover them.
            await MeetingTableOverlapGuard.EnsureTableIsFreeAsync(
                appDbContext, table.Id, request.Start, request.End,
                ErrorCodes.BusinessMeetingTableConflict,
                excludeDelegationRequestId: null,
                excludeSpeakerRequestId: null,
                excludeBusinessMeetingId: meeting.Id,
                cancellationToken);

            // Hall conflict — the table's hall is wholly reserved for a non-meeting
            // purpose (e.g. a session) for an overlapping slot (FDS-013 §5.6: a
            // whole-hall allocation is a unit that cannot be double-reserved).
            var hallReserved = await appDbContext.HallAllocations.AsNoTracking()
                .Where(a => a.HallId == table.HallId
                    && a.ReleasedAt == null
                    && a.Mode == HallAllocationMode.Whole
                    && a.Purpose != HallPurpose.Meeting)
                .AnyAsync(a => a.Start < request.End && request.Start < a.End,
                    cancellationToken);
            if (hallReserved)
            {
                throw new ApiException(
                    ErrorCodes.BusinessMeetingTableConflict, 409,
                    "The hall is reserved for another purpose at this time.",
                    "القاعة محجوزة لغرض آخر في هذا الوقت.");
            }

            // Participant conflict — a party is already in a Confirmed overlapping meeting.
            var partyClash = await appDbContext.BusinessMeetingParticipants.AsNoTracking()
                .Where(p => (p.ExhibitorId != null && companyIds.Contains(p.ExhibitorId.Value))
                    || (p.VisitorUserId != null && visitorIds.Contains(p.VisitorUserId.Value)))
                .Join(appDbContext.BusinessMeetings.AsNoTracking()
                        .Where(m => m.Status == BusinessMeetingStatus.Confirmed),
                    p => p.BusinessMeetingId, m => m.Id, (p, m) => m)
                .AnyAsync(m => m.Start < request.End && request.Start < m.End,
                    cancellationToken);
            if (partyClash)
            {
                throw new ApiException(
                    ErrorCodes.BusinessMeetingParticipantConflict, 409,
                    "A participant already has a meeting at this time.",
                    "أحد المشاركين لديه اجتماع بالفعل في هذا الوقت.");
            }

            appDbContext.BusinessMeetings.Add(meeting);
            await appDbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BusinessMeetingScheduled,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"meetingId={meeting.Id}; tableId={table.Id}; type={request.MeetingType}; "
                + $"participants={parties.Count}; from={request.Start:o}; to={request.End:o}",
        }, cancellationToken);

        await NotifyParticipantsAsync(meeting, NotificationKind.MeetingScheduled,
            "A meeting was scheduled for you", "تم تحديد موعد اجتماع لك", cancellationToken);

        return new BusinessMeetingScheduled(
            meeting.Id, meeting.Status, meeting.Start, meeting.End);
    }

    public async Task CancelMeetingAsync(
        Guid actorUserId, Guid id, CancelMeetingRequest request,
        CancellationToken cancellationToken = default)
    {
        var meeting = await appDbContext.BusinessMeetings
            .Include(m => m.Participants)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw NotFound(ErrorCodes.BusinessMeetingNotFound,
                "Meeting not found.", "لم يتم العثور على الاجتماع.");
        if (meeting.Status != BusinessMeetingStatus.Confirmed)
        {
            throw new ApiException(
                ErrorCodes.BusinessMeetingNotConfirmed, 409,
                "This meeting is not confirmed.",
                "هذا الاجتماع غير مؤكد.");
        }

        var now = timeProvider.SimfNow();
        meeting.Status = BusinessMeetingStatus.Cancelled;
        meeting.CancelledByUserId = actorUserId;
        meeting.CancelledAt = now;
        meeting.CancellationReason = Trim(request.Reason);
        meeting.UpdatedAt = now;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BusinessMeetingCancelled,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"meetingId={id}; tableId={meeting.MeetingTableId}",
        }, cancellationToken);

        await NotifyParticipantsAsync(meeting, NotificationKind.MeetingCancelled,
            "A meeting was cancelled", "تم إلغاء اجتماع", cancellationToken);
    }

    // ── internals ─────────────────────────────────────────────────────────────

    private async Task<Domain.Programme.Hall> EnsureMeetingHallAsync(
        Guid hallId, CancellationToken cancellationToken)
    {
        var hall = await appDbContext.Halls
            .SingleOrDefaultAsync(h => h.Id == hallId && h.IsActive, cancellationToken)
            ?? throw NotFound(ErrorCodes.HallNotFound, "Hall not found.", "لم يتم العثور على القاعة.");
        if (hall.Purpose is not (HallPurpose.Meeting or HallPurpose.General))
        {
            throw new ApiException(
                ErrorCodes.HallNotMeetingPurpose, 409,
                "Meeting tables require a Meeting or General hall.",
                "تتطلب طاولات الاجتماعات قاعة من نوع اجتماعات أو عامة.");
        }
        return hall;
    }

    private async Task EnsureCodeFreeAsync(
        Guid hallId, string code, CancellationToken cancellationToken)
    {
        var clash = await appDbContext.MeetingTables.AsNoTracking()
            .AnyAsync(t => t.HallId == hallId && t.IsActive
                && t.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.MeetingTableCodeDuplicate, 409,
                $"A table with code '{code}' already exists in this hall.",
                $"توجد طاولة بالرمز '{code}' في هذه القاعة بالفعل.");
        }
    }

    /// <summary>Resolve each party's display name into a snapshot keyed by
    /// "C:{companyId}" / "V:{userId}". Companies from the App DB (Arabic name
    /// primary); visitors via a single Identity round-trip (no cross-DB JOIN).</summary>
    private async Task<Dictionary<string, string>> ResolvePartyNamesAsync(
        IReadOnlyList<Guid> companyIds, IReadOnlyList<Guid> visitorIds,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>();

        if (companyIds.Count > 0)
        {
            var companies = await appDbContext.Exhibitors.AsNoTracking()
                .Where(c => companyIds.Contains(c.Id) && c.IsActive)
                .Select(c => new { c.Id, c.NameArabic, c.Name })
                .ToListAsync(cancellationToken);
            if (companies.Count != companyIds.Distinct().Count())
            {
                throw Invalid(ErrorCodes.MeetingParticipantInvalid,
                    "One or more exhibitors were not found.",
                    "تعذر العثور على عارض واحد أو أكثر.");
            }
            foreach (var c in companies)
            {
                names[$"C:{c.Id}"] = string.IsNullOrWhiteSpace(c.NameArabic) ? c.Name : c.NameArabic;
            }
        }

        if (visitorIds.Count > 0)
        {
            // Only an approved Visitor account may be a visitor party (the CP picker
            // already feeds approved visitors; this guards a hand-crafted request
            // from seating, e.g., an Admin as a "visitor"). The count-mismatch check
            // below then rejects any id that is not an approved visitor.
            var users = await identityDbContext.Users.AsNoTracking()
                .Where(u => visitorIds.Contains(u.Id)
                    && u.UserType == UserType.Visitor
                    && u.AccountState == AccountState.Approved)
                .Select(u => new { u.Id, u.DisplayName })
                .ToListAsync(cancellationToken);
            if (users.Count != visitorIds.Distinct().Count())
            {
                throw Invalid(ErrorCodes.MeetingParticipantInvalid,
                    "One or more visitors were not found or not approved.",
                    "تعذر العثور على زائر واحد أو أكثر أو أنه غير معتمد.");
            }
            foreach (var u in users)
            {
                names[$"V:{u.Id}"] = u.DisplayName ?? string.Empty;
            }
        }

        return names;
    }

    /// <summary>Notify every participant — visitors directly, company parties via
    /// each active ExhibitorMembership account. Notification failure never rolls back
    /// the meeting (swallow-and-log).</summary>
    private async Task NotifyParticipantsAsync(
        BusinessMeeting meeting, NotificationKind kind,
        string title, string titleArabic, CancellationToken cancellationToken)
    {
        try
        {
            var recipients = new HashSet<Guid>();
            foreach (var p in meeting.Participants)
            {
                if (p.Kind == MeetingPartyKind.Visitor && p.VisitorUserId is { } uid)
                {
                    recipients.Add(uid);
                }
            }
            var companyIds = meeting.Participants
                .Where(p => p.Kind == MeetingPartyKind.Company && p.ExhibitorId != null)
                .Select(p => p.ExhibitorId!.Value).Distinct().ToList();
            if (companyIds.Count > 0)
            {
                var memberUserIds = await appDbContext.ExhibitorMemberships.AsNoTracking()
                    .Where(m => companyIds.Contains(m.ExhibitorId) && m.IsActive)
                    .Select(m => m.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var uid in memberUserIds) recipients.Add(uid);
            }

            var severity = kind == NotificationKind.MeetingCancelled
                ? NotificationSeverity.Warning : NotificationSeverity.Info;
            foreach (var uid in recipients)
            {
                await notifications.DispatchAsync(new NotificationRequest
                {
                    UserId = uid,
                    Kind = kind,
                    Title = title,
                    TitleArabic = titleArabic,
                    Body = $"Table meeting on {meeting.Start.FormatSaudi()}.",
                    BodyArabic = $"اجتماع طاولة بتاريخ {meeting.Start.FormatSaudi()}.",
                    Severity = severity,
                    RelatedEntityType = "BusinessMeeting",
                    RelatedEntityId = meeting.Id,
                    SendEmail = false,
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Participant notification ({Kind}) failed for meeting {MeetingId}",
                kind, meeting.Id);
        }
    }

    private List<Party> NormaliseParticipants(IReadOnlyList<ScheduleMeetingParticipant> input)
    {
        if (input is null || input.Count == 0 || input.Count > MaxParticipants)
        {
            throw Invalid(ErrorCodes.MeetingParticipantInvalid,
                $"Provide 1–{MaxParticipants} participants.",
                $"يرجى إدخال من 1 إلى {MaxParticipants} مشاركاً.");
        }

        var seen = new HashSet<string>();
        var parties = new List<Party>();
        foreach (var p in input)
        {
            if (p.Kind == MeetingPartyKind.Company)
            {
                if (p.CompanyId is not { } cid || cid == Guid.Empty)
                {
                    throw Invalid(ErrorCodes.MeetingParticipantInvalid,
                        "A company participant needs a company.",
                        "يحتاج المشارك من نوع شركة إلى شركة.");
                }
                var key = $"C:{cid}";
                if (seen.Add(key)) parties.Add(new Party(MeetingPartyKind.Company, cid, null, key));
            }
            else
            {
                if (p.VisitorUserId is not { } vid || vid == Guid.Empty)
                {
                    throw Invalid(ErrorCodes.MeetingParticipantInvalid,
                        "A visitor participant needs a user.",
                        "يحتاج المشارك من نوع زائر إلى مستخدم.");
                }
                var key = $"V:{vid}";
                if (seen.Add(key)) parties.Add(new Party(MeetingPartyKind.Visitor, null, vid, key));
            }
        }
        return parties;
    }

    private MeetingTable NewTable(
        Guid hallId, string code, string? row, int? col, int capacity, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            HallId = hallId,
            Code = code,
            RowLabel = row,
            ColumnNumber = col,
            Capacity = capacity,
            IsActive = true,
            CreatedAt = now,
        };

    private static MeetingTableRow ToTableRow(MeetingTable t) =>
        new(t.Id, t.HallId, t.Code, t.RowLabel, t.ColumnNumber, t.Capacity, t.IsActive);

    private static (int Skip, int Top) Page(GridQuery query) =>
        query.ClampPage(50, 500);

    private async Task ValidateSlotAsync(
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        if (end <= start)
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "The end time must be after the start time.",
                "يجب أن يكون وقت النهاية بعد وقت البداية.");
        }

        // M-5 — lower time bound: a meeting / allocation cannot start in the past.
        if (start < timeProvider.SimfNow())
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "The start time cannot be in the past.",
                "لا يمكن أن يكون وقت البداية في الماضي.");
        }

        // D-753 — forum-day bound: a meeting / allocation may only be scheduled on
        // the authored event days. The window is MIN/MAX over the active
        // ProgrammeDay.Date rows (NOT the stale OrganizationProfile placeholder). The
        // slot's start and end are converted to the event-local (+03:00) calendar
        // date — the same convention the programme uses to bucket a session to a day
        // — and both must fall inside [MinDate, MaxDate]. When no programme days are
        // seeded yet the window is null and no bound is applied (scheduling is never
        // hard-blocked just because content is not seeded).
        var forum = await forumWindow.GetForumDaysAsync(cancellationToken);
        if (forum is { } window)
        {
            var startDate = DateOnly.FromDateTime(start);
            var endDate = DateOnly.FromDateTime(end);
            if (startDate < window.MinDate || endDate > window.MaxDate)
            {
                throw Invalid(ErrorCodes.HallAllocationInvalid,
                    $"Meetings can only be scheduled within the forum days "
                        + $"({window.MinDate:dd-MM-yyyy} to {window.MaxDate:dd-MM-yyyy}).",
                    $"لا يمكن جدولة الاجتماعات إلا خلال أيام الملتقى "
                        + $"({window.MinDate:dd-MM-yyyy} إلى {window.MaxDate:dd-MM-yyyy}).");
            }
        }
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity is < 2 or > 100)
        {
            throw Invalid(ErrorCodes.MeetingTableInvalid,
                "Table capacity must be between 2 and 100.",
                "يجب أن تكون سعة الطاولة بين 2 و 100.");
        }
    }

    private static string NormaliseCode(string? code)
    {
        var trimmed = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (trimmed.Length is < 1 or > 16)
        {
            throw Invalid(ErrorCodes.MeetingTableInvalid,
                "A table code of 1–16 characters is required.",
                "يلزم إدخال رمز طاولة من 1 إلى 16 حرفاً.");
        }
        return trimmed;
    }

    private static string? Trim(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static List<string> ParseCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    /// <summary>Split a row/column code like "A12" into ("A", 12). Returns
    /// (code-as-row, null) when there is no trailing number.</summary>
    private static (string? Row, int? Col) SplitRowColumn(string code)
    {
        var i = 0;
        while (i < code.Length && !char.IsDigit(code[i])) i++;
        if (i == 0 || i == code.Length) return (code.Length <= 8 ? code : null, null);
        var row = code[..i];
        return int.TryParse(code[i..], out var col)
            ? (row.Length <= 8 ? row : null, col)
            : (row.Length <= 8 ? row : null, null);
    }

    private static ApiException NotFound(string code, string en, string ar) =>
        new(code, 404, en, ar);

    private static ApiException Invalid(string code, string en, string ar) =>
        new(code, 400, en, ar);

    private readonly record struct Party(
        MeetingPartyKind Kind, Guid? CompanyId, Guid? VisitorUserId, string Key);
}
