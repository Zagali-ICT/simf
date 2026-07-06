// Tests: covered via the SIMF.Api.Tests service suites that consume it —
// Speaker/Delegation/BadgeUpdate/ParticipationDocumentRequestsTests (GetEmailAsync),
// AdminGatesTests, AdminInvitationsTests, ExhibitorVisitorScanTests,
// VisitorContactSharingTests (GetDisplayNamesAsync / GetEmailsAsync).
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

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        await identityDbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName ?? string.Empty, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string?>> GetEmailsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        await identityDbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);
}
