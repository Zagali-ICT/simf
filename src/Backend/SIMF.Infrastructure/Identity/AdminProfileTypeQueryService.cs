using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-100: first slice of the R2 follow-up — extracts the read-only
/// <see cref="IAdminProfileTypeQueryService.ListProfileTypesAsync"/> method
/// out of the 1091-line <see cref="AdminAccountService"/> aggregate into
/// its own focused implementation. The remaining 4 interfaces
/// (<see cref="IAdminTwoFactorService"/>, <see cref="IAdminUserApprovalService"/>,
/// <see cref="IAdminUserProvisioningService"/>, <see cref="IAdminUserBulkService"/>)
/// stay co-resident on <c>AdminAccountService</c> for now — they share
/// rich private helpers and splitting them safely is a separate refactor
/// session.
///
/// <para>This slice is the proof-of-pattern: each future split follows
/// the same shape — its own class file, no shared private helpers, DI
/// registration moves from the factory-delegate (R2 — D-075) to a
/// direct <c>AddScoped&lt;Interface, Impl&gt;</c>.</para>
/// </summary>
internal sealed class AdminProfileTypeQueryService(
    SimfIdentityDbContext dbContext)
    : IAdminProfileTypeQueryService
{
    public async Task<IReadOnlyList<AdminProfileTypeSummary>> ListProfileTypesAsync(
        UserType userType, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProfileTypes
            .Where(profileType => profileType.UserType == userType && profileType.IsActive)
            .OrderBy(profileType => profileType.Name)
            .Select(profileType => new AdminProfileTypeSummary(
                profileType.Id,
                profileType.Name,
                profileType.NameArabic,
                profileType.PageColor,
                profileType.UserType.ToString(),
                profileType.IsActive))
            .ToListAsync(cancellationToken);
    }
}
