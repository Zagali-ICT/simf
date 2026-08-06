// Tests: SIMF.Api.Tests/MediaPartnersTests.cs
using SIMF.Contracts.PublicRelations;

namespace SIMF.Application.PublicRelations.Abstractions;

/// <summary>Read-only public projection of active
/// media partners for the mobile app + website. The service returns active
/// rows ordered by (DisplayOrder asc, NameAr asc).</summary>
public interface IPublicMediaPartnerService
{
    Task<PublicMediaPartners> ListAsync(CancellationToken cancellationToken = default);
}
