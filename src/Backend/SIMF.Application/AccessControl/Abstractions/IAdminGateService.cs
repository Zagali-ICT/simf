using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>Admin CRUD over <c>Gate</c> + per-gate assignments
/// and allowed profile types (SIMF-API-GATES-001 §6). Backed by
/// <c>AdminGateService</c> in the Infrastructure layer.</summary>
public interface IAdminGateService
{
    Task<GridPage<AdminGateSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminGateDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminGateDetail> CreateAsync(
        Guid actorUserId, AdminCreateGateRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminGateDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateGateRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>The active operators on ONE gate. Deliberately not a grid: the set is
    /// bounded by the gate, and the same set round-trips whole as
    /// <c>AssignedOperatorUserIds</c> on the create / update body, so a gate whose
    /// roster needed paging could not be edited in the first place.</summary>
    Task<IReadOnlyList<AdminGateAssignmentRow>> ListAssignmentsAsync(
        Guid gateId, CancellationToken cancellationToken = default);

    /// <summary>BUG-018 — the accounts that may be assigned as gate operators:
    /// approved APP accounts whose profile type is operational
    /// (<c>IsForVisitor=false</c>) and carries a MobileAppRole that confers
    /// <c>Gates.Operate</c>. Searchable + paged, so the CP picker is not a blind
    /// top-200 of admin accounts.</summary>
    Task<GridPage<AdminGateOperatorCandidate>> ListOperatorCandidatesAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>BUG-018 — the gate form's allowed-profile-type + hall lookups,
    /// served under <c>Gates.Manage</c> so a gate manager who does not hold
    /// <c>ProfileTypes.View</c> / <c>Halls.View</c> still gets populated
    /// dropdowns.</summary>
    Task<AdminGateFormOptions> GetFormOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>One page of the scan report over <c>GateScans</c>, the highest-write
    /// table in the system. Filter, search, sort and page window are all validated
    /// against the service's declared columns, so an unknown key is a 400 rather than
    /// a silently unfiltered read of the whole log.</summary>
    Task<GridPage<AdminGateScanRow>> ListScansAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One page of the occupancy report — one row per visitor whose latest
    /// allowed scan is a check-in inside the presence window. <c>Total</c> is the true
    /// occupancy, so the dashboard's stat card stays correct while the table shows
    /// one page of it.</summary>
    Task<GridPage<AdminCurrentlyInsideRow>> ListCurrentlyInsideAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>The scan report as XLSX. Takes the same <see cref="GridQuery"/> as
    /// <see cref="ListScansAsync"/> so the workbook can never drift out of filter
    /// parity with the grid it was exported from; only the row bound differs.</summary>
    Task<byte[]> ExportScansXlsxAsync(
        GridQuery query, CancellationToken cancellationToken = default);
}
