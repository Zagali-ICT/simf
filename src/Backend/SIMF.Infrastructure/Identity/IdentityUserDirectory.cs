using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Reads Identity-owned user attributes from <c>SimfIdentityDbContext</c> for
/// App-side callers that reference a user by its logical (bare-<c>Guid</c>) id
/// across the DB boundary (D-157). One context per call, no cross-database JOIN.
/// </summary>
internal sealed class IdentityUserDirectory(SimfIdentityDbContext identityDbContext)
    : IIdentityUserDirectory
{
    public Task<string?> GetEmailAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);
}
