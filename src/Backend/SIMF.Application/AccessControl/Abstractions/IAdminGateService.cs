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

    Task<IReadOnlyList<AdminGateScanRow>> ListScansAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminCurrentlyInsideRow>> ListCurrentlyInsideAsync(
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportScansXlsxAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default);
}
