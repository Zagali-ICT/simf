using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Read-side query for the ProfileType lookup table — admin CP create
/// pages populate their subtype dropdown from here.
/// Split out of <c>IAdminAccountService</c>: it was the read-side concern
/// that was the odd one out among the command-shaped methods.
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
