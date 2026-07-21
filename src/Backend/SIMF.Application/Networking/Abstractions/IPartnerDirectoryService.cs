using SIMF.Contracts.Networking;

namespace SIMF.Application.Networking.Abstractions;

/// <summary>Build #13 — the "Meet People Like You" partner directory: the deduped
/// union of curated Sponsors, Speakers and Booth companies plus the opted-in
/// "Other"-type (non-visitor) user accounts. Backs
/// <c>GET /api/v1/app/networking/partner-directory</c>.</summary>
public interface IPartnerDirectoryService
{
    /// <summary>Returns the directory, or an empty list when the CP switch
    /// <c>OrganizationProfile.PartnerDirectoryEnabled</c> is off.</summary>
    Task<PartnerDirectoryResponse> GetAsync(CancellationToken cancellationToken = default);
}
