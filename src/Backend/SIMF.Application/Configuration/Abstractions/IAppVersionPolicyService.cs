using SIMF.Contracts.Configuration;

namespace SIMF.Application.Configuration.Abstractions;

/// <summary>The public read-path over the whitelisted
/// <c>AppUpdateSettingKeys</c>: the per-platform minimum/latest app versions +
/// store URLs the mobile app checks on launch. Read-only; admin writes go
/// through <see cref="IAdminSystemSettingService"/> (the CP configuration
/// page).</summary>
public interface IAppVersionPolicyService
{
    Task<AppVersionPolicyResponse> GetAsync(CancellationToken cancellationToken = default);
}
