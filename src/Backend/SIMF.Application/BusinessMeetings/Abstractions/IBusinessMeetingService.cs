using SIMF.Common;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.Application.BusinessMeetings.Abstractions;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — the flexible hall-configuration +
/// admin-arranged B2B/B2C business-meeting service. Covers: setting a hall's
/// purpose, defining / generating meeting tables, reserving hall space
/// (whole / random-by-count / row-column over a time-slot), and scheduling /
/// cancelling meetings between two or more parties (companies + visitors).
/// All operations are Control-Panel (admin) only.
/// </summary>
public interface IBusinessMeetingService
{
    // ── Hall purpose ─────────────────────────────────────────────────────────
    Task SetHallPurposeAsync(
        Guid actorUserId, Guid hallId, SetHallPurposeRequest request,
        CancellationToken cancellationToken = default);

    // ── Meeting tables ───────────────────────────────────────────────────────
    Task<GridPage<MeetingTableRow>> ListTablesAsync(
        Guid hallId, GridQuery query, CancellationToken cancellationToken = default);

    Task<MeetingTableRow> CreateTableAsync(
        Guid actorUserId, Guid hallId, CreateMeetingTableRequest request,
        CancellationToken cancellationToken = default);

    Task<MeetingTableRow> UpdateTableAsync(
        Guid actorUserId, Guid tableId, UpdateMeetingTableRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteTableAsync(
        Guid actorUserId, Guid tableId, CancellationToken cancellationToken = default);

    Task<MeetingTablesGenerated> GenerateTablesAsync(
        Guid actorUserId, Guid hallId, GenerateMeetingTablesRequest request,
        CancellationToken cancellationToken = default);

    // ── Hall allocations ─────────────────────────────────────────────────────
    Task<GridPage<HallAllocationRow>> ListAllocationsAsync(
        Guid hallId, GridQuery query, CancellationToken cancellationToken = default);

    Task<HallAllocationRow> CreateAllocationAsync(
        Guid actorUserId, Guid hallId, CreateHallAllocationRequest request,
        CancellationToken cancellationToken = default);

    Task ReleaseAllocationAsync(
        Guid actorUserId, Guid allocationId, CancellationToken cancellationToken = default);

    // ── Business meetings ────────────────────────────────────────────────────
    Task<GridPage<BusinessMeetingRow>> ListMeetingsAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<BusinessMeetingDetail> GetMeetingAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<BusinessMeetingScheduled> ScheduleMeetingAsync(
        Guid actorUserId, ScheduleMeetingRequest request,
        CancellationToken cancellationToken = default);

    Task CancelMeetingAsync(
        Guid actorUserId, Guid id, CancelMeetingRequest request,
        CancellationToken cancellationToken = default);
}
