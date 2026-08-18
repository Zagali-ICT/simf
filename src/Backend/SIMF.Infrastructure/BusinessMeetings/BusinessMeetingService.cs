// Tests: SIMF.Api.Tests/BusinessMeetingsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.BusinessMeetings.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Domain.BusinessMeetings;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.MeetingRequests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.BusinessMeetings;

/// <summary>Flexible hall configuration + admin-arranged
/// B2B/B2C business meetings. Halls carry a <see cref="HallPurpose"/>; Meeting /
/// General halls hold <see cref="MeetingTable"/>s (added one-by-one or generated
/// random-by-count / by row-column); <see cref="HallAllocation"/> reserves hall
/// space over a from–to slot; a <see cref="BusinessMeeting"/> schedules two or more
/// parties (companies + visitors) at a table. Visitor names resolve via a second
/// Identity round-trip (no cross-DB JOIN); company refs are App FKs.</summary>
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallPurposeChanged,
            actorUserId,
            $"hallId={hallId}; purpose={request.Purpose}",
            cancellationToken);
    }

    // ── Meeting tables ───────────────────────────────────────────────────────

    /// <summary>The grid contract for the meeting-tables half of
    /// /admin/meeting-tables. The page declares no sortable or filterable column, so
    /// the only key that reaches here is the <c>hallId</c> the Excel export carries
    /// to name the hall (see <c>ExportMeetingTablesEndpoint</c>). It repeats the
    /// scope predicate below and so cannot widen the set — but undeclared it would
    /// 400 every export.</summary>
    private static readonly GridColumns<MeetingTable> TableColumns = new GridColumns<MeetingTable>()
        .Add("code", table => table.Code)
        .Add("hallId", table => table.HallId)
        .DefaultOrder("code")
        .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<MeetingTable, MeetingTableRow>> ToTableGridRow =
        table => new MeetingTableRow(
            table.Id, table.HallId, table.Code, table.RowLabel,
            table.ColumnNumber, table.Capacity, table.IsActive);

    public Task<GridPage<MeetingTableRow>> ListTablesAsync(
        Guid hallId, GridQuery query, CancellationToken cancellationToken = default) =>
        // The hall scope and the soft-delete rule are the resource, not the request,
        // so they compose ahead of the grid and no filter can override them.
        appDbContext.MeetingTables
            .Where(table => table.HallId == hallId && table.IsActive)
            .ToGridPageAsync(
                query, TableColumns, table => table.Id, ToTableGridRow, cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.MeetingTableCreated,
            actorUserId,
            $"tableId={table.Id}; hallId={hallId}; code={code}; capacity={request.Capacity}",
            cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.MeetingTableUpdated,
            actorUserId,
            $"tableId={tableId}; code={code}; capacity={request.Capacity}",
            cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.MeetingTableDeactivated,
            actorUserId,
            $"tableId={tableId}; hallId={table.HallId}",
            cancellationToken);
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.MeetingTablesGenerated,
            actorUserId,
            $"hallId={hallId}; mode={request.Mode}; created={toCreate.Count}; "
                + $"removed={removed}; reset={request.Reset}",
            cancellationToken);

        return new MeetingTablesGenerated(toCreate.Count, removed);
    }

    // ── Hall allocations ─────────────────────────────────────────────────────

    /// <summary>The grid contract for the allocations half of
    /// /admin/meeting-tables. The page declares no sortable or filterable column and
    /// there is no allocations export, so no key reaches here today; <c>start</c> is
    /// declared because the natural order names it.</summary>
    private static readonly GridColumns<HallAllocation> AllocationColumns =
        new GridColumns<HallAllocation>()
            .Add("start", allocation => allocation.Start)
            .DefaultOrder("start", descending: true)
            .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<HallAllocation, HallAllocationRow>> ToAllocationRow =
        allocation => new HallAllocationRow(
            allocation.Id, allocation.HallId, allocation.Purpose, allocation.Mode,
            allocation.UnitCount, allocation.RowColumnSpec,
            allocation.Start, allocation.End, allocation.Notes);

    public Task<GridPage<HallAllocationRow>> ListAllocationsAsync(
        Guid hallId, GridQuery query, CancellationToken cancellationToken = default) =>
        // The hall scope and the released-rows rule are the resource, not the
        // request, so they compose ahead of the grid.
        appDbContext.HallAllocations
            .Where(allocation => allocation.HallId == hallId && allocation.ReleasedAt == null)
            .ToGridPageAsync(
                query, AllocationColumns, allocation => allocation.Id,
                ToAllocationRow, cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallAllocationCreated,
            actorUserId,
            $"allocationId={allocation.Id}; hallId={hallId}; purpose={request.Purpose}; "
                + $"mode={request.Mode}; from={request.Start:o}; to={request.End:o}",
            cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.HallAllocationReleased,
            actorUserId,
            $"allocationId={allocationId}; hallId={allocation.HallId}",
            cancellationToken);
    }

    // ── Business meetings ────────────────────────────────────────────────────

    /// <summary>
    /// The grid contract for /admin/business-meetings: one entry per key
    /// BusinessMeetingsList.razor can send, as both its filter and its sort. A key
    /// not declared here is a 400, not a silently ignored request.
    ///
    /// <para>
    /// The hall and table keys read through the meeting's navigations, which are all
    /// App-DB columns and so translate. Nothing here names
    /// <c>Participants.Count</c>: that is a correlated subquery EF Core cannot put
    /// in an ORDER BY, and it stays where it is legal, in the projection.
    /// </para>
    /// </summary>
    private static readonly GridColumns<BusinessMeeting> MeetingColumns =
        new GridColumns<BusinessMeeting>()
            .Add("hall", meeting => meeting.MeetingTable!.Hall!.NameArabic)
            .Add("table", meeting => meeting.MeetingTable!.Code)
            .Add("type", meeting => meeting.MeetingType)
            .Add("start", meeting => meeting.Start)
            .Add("end", meeting => meeting.End)
            .Add("status", meeting => meeting.Status)
            // Most recent start first.
            .DefaultOrder("start", descending: true)
            .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<BusinessMeeting, BusinessMeetingRow>> ToMeetingRow =
        meeting => new BusinessMeetingRow(
            meeting.Id, meeting.MeetingTableId, meeting.MeetingTable!.Code,
            meeting.MeetingTable.HallId, meeting.MeetingTable.Hall!.NameArabic,
            meeting.MeetingType, meeting.Start, meeting.End, meeting.Status,
            meeting.Participants.Count);

    public Task<GridPage<BusinessMeetingRow>> ListMeetingsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.BusinessMeetings.ToGridPageAsync(
            query, MeetingColumns, meeting => meeting.Id, ToMeetingRow, cancellationToken);

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

        // Close the read-then-insert double-book race. A time range cannot be a
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

            // Table conflict — the table is already held over this slot. The
            // scan used to see this service's OWN family only, so a business meeting
            // could be scheduled onto a table already held by a delegation or speaker
            // meeting request. The shared guard covers all three families; running it
            // here keeps its range scans inside this Serializable transaction, so the
            // key-range locks that close the double-booking race still cover them.
            await MeetingTableOverlapGuard.EnsureTableIsFreeAsync(
                appDbContext, table.Id, request.Start, request.End,
                ErrorCodes.BusinessMeetingTableConflict,
                excludeDelegationRequestId: null,
                excludeSpeakerRequestId: null,
                excludeBusinessMeetingId: meeting.Id,
                cancellationToken);

            // Hall conflict — the table's hall is wholly reserved for a non-meeting
            // purpose (e.g. a session) for an overlapping slot: a
            // whole-hall allocation is a unit that cannot be double-reserved.
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.BusinessMeetingScheduled,
            actorUserId,
            $"meetingId={meeting.Id}; tableId={table.Id}; type={request.MeetingType}; "
                + $"participants={parties.Count}; from={request.Start:o}; to={request.End:o}",
            cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.BusinessMeetingCancelled,
            actorUserId,
            $"meetingId={id}; tableId={meeting.MeetingTableId}",
            cancellationToken);

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

    /// <summary>The in-memory twin of <see cref="ToTableGridRow"/>, for the create /
    /// update paths that already hold the entity. The grid's copy has to stay an
    /// <c>Expression</c> so EF can translate it, which is why the mapping is written
    /// out twice; keep the two field orders identical.</summary>
    private static MeetingTableRow ToTableRow(MeetingTable table) =>
        new(table.Id, table.HallId, table.Code, table.RowLabel,
            table.ColumnNumber, table.Capacity, table.IsActive);

    private async Task ValidateSlotAsync(
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        if (end <= start)
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "The end time must be after the start time.",
                "يجب أن يكون وقت النهاية بعد وقت البداية.");
        }

        // Lower time bound: a meeting / allocation cannot start in the past.
        if (start < timeProvider.SimfNow())
        {
            throw Invalid(ErrorCodes.HallAllocationInvalid,
                "The start time cannot be in the past.",
                "لا يمكن أن يكون وقت البداية في الماضي.");
        }

        // Forum-day bound: a meeting / allocation may only be scheduled on
        // the authored event days. The window is MIN/MAX over the active
        // ProgrammeDay.Date rows (NOT the stale OrganizationProfile placeholder), and
        // both ends of the slot must fall inside [MinDate, MaxDate]. No time-zone
        // shift is applied on the way to a date: the slot is already event-local
        // (KSA, +03:00), the same convention the programme uses to bucket a session
        // to a Riyadh calendar day, so a late-evening slot files under the correct
        // event day without one. When no programme days are seeded yet the window is
        // null and no bound is applied (scheduling is never hard-blocked just
        // because content is not seeded).
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
