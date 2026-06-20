using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Configuration.Abstractions;

/// <summary>P2.4 — D-229 (FDS-012 §5.5): admin CRUD over the platform
/// system-settings store. Ships empty; the team seeds the keys (FDS-012 OI-2).</summary>
public interface IAdminSystemSettingService
{
    Task<GridPage<AdminSystemSettingSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminSystemSettingDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminSystemSettingDetail> CreateAsync(
        Guid actorUserId, AdminCreateSystemSettingRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminSystemSettingDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSystemSettingRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>D-464 — upsert the CP "Site Settings" page values (registration
    /// welcome message + social links) into their <c>SiteSettingKeys</c> keys.
    /// Creates a key if absent, updates it otherwise; blank clears a value.</summary>
    Task SaveSiteSettingsAsync(
        Guid actorUserId, AdminUpdateSiteSettingsRequest request,
        CancellationToken cancellationToken = default);
}
