using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>D-148 — admin CRUD over <c>Gate</c> + per-gate assignments
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

    Task<IReadOnlyList<AdminGateScanRow>> ListScansAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminCurrentlyInsideRow>> ListCurrentlyInsideAsync(
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportScansXlsxAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default);
}
