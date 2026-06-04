using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Sponsors.Abstractions;

/// <summary>D-199 (Mockup page 23) — admin CRUD over <c>Sponsor</c>.
/// Mirrors IAdminDelegationService / IAdminCountryService.</summary>
public interface IAdminSponsorService
{
    Task<GridPage<AdminSponsorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminSponsorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminSponsorDetail> CreateAsync(
        Guid actorUserId, AdminCreateSponsorRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminSponsorDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSponsorRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
