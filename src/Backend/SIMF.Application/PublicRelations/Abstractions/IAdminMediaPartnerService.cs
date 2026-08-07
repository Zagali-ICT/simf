// Tests: SIMF.Api.Tests/AdminMediaPartnersTests.cs
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Application.PublicRelations.Abstractions;

/// <summary>Admin CRUD contract over
/// <see cref="Contracts.PublicRelations.AdminMediaPartnerSummary"/>.
/// Mirrors <c>IAdminCountryService</c> / <c>IAdminSpeakerService</c>.</summary>
public interface IAdminMediaPartnerService
{
    Task<GridPage<AdminMediaPartnerSummary>> ListAllAsync(GridQuery query, CancellationToken cancellationToken = default);
    Task<AdminMediaPartnerDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminMediaPartnerDetail> CreateAsync(Guid actorUserId, AdminCreateMediaPartnerRequest request, CancellationToken cancellationToken = default);
    Task<AdminMediaPartnerDetail> UpdateAsync(Guid actorUserId, Guid id, AdminUpdateMediaPartnerRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}
