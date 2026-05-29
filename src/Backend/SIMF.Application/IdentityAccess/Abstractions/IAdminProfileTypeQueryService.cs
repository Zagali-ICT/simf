using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Read-side query for the ProfileType lookup table — admin CP create
/// pages populate their subtype dropdown from here (P7c — D-048). R2 —
/// D-075: split out of <c>IAdminAccountService</c> per Architecture
/// SEV-1.2 (the read-side concern that was the odd one out among the
/// command-shaped methods).
/// </summary>
public interface IAdminProfileTypeQueryService
{
    /// <summary>
    /// Returns every active <c>ProfileType</c> row for the given
    /// <paramref name="userType"/>.
    /// </summary>
    Task<IReadOnlyList<AdminProfileTypeSummary>> ListProfileTypesAsync(
        UserType userType,
        CancellationToken cancellationToken = default);
}
