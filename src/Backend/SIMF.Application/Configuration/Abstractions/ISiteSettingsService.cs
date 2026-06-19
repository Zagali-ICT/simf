using SIMF.Contracts.Configuration;

namespace SIMF.Application.Configuration.Abstractions;

/// <summary>D-461 — the public read-path over the whitelisted
/// <c>SiteSettingKeys</c>. Resolves the configured values from the
/// system-settings store, falling back to the in-code defaults. Read-only;
/// admin writes go through <see cref="IAdminSystemSettingService"/>.</summary>
public interface ISiteSettingsService
{
    Task<SiteSettingsResponse> GetAsync(CancellationToken cancellationToken = default);
}
