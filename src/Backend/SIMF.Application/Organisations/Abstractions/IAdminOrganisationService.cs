using SIMF.Common;
using SIMF.Contracts.Organisations;

namespace SIMF.Application.Organisations.Abstractions;

/// <summary>Admin CRUD plus bulk Excel import over <c>Organisation</c>
/// (bilingual Saudi-companies lookup). Mirrors IAdminBoothService.</summary>
public interface IAdminOrganisationService
{
    /// <summary>One page of organisation summaries for the admin grid.</summary>
    Task<GridPage<AdminOrganisationSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default);

    /// <summary>One organisation by id, or null when it is missing.</summary>
    Task<AdminOrganisationDetail?> GetAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>Creates a new organisation and returns its full detail.</summary>
    Task<AdminOrganisationDetail> CreateAsync(
        Guid actorUserId, CreateOrganisationRequest request,
        CancellationToken ct = default);

    /// <summary>Updates an existing organisation and returns its full detail.</summary>
    Task<AdminOrganisationDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateOrganisationRequest request,
        CancellationToken ct = default);

    /// <summary>Soft-deletes (deactivates) an organisation.</summary>
    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken ct = default);

    /// <summary>Imports organisations from a government .xlsx workbook,
    /// inserting new rows and updating matched ones, and returns a per-run
    /// summary (rows read / inserted / updated / skipped + errors).</summary>
    Task<OrganisationImportResult> ImportAsync(
        Guid actorUserId, Stream xlsxStream,
        CancellationToken ct = default);
}
