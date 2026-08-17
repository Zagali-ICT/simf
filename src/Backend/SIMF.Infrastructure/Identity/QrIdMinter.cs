// Tests: SIMF.Api.Tests/UserProfileTests.cs (QR minted on admin-create-user;
//        not minted before Approved).
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Crockford base32 12-character QR-id minter. 32^12 ≈
/// 1.2 × 10^18 possible values; collisions are negligible at SIMF scale
/// (thousands of users), but we still query the DB once to ensure
/// uniqueness — the column carries a UNIQUE constraint anyway.
///
/// <para>Minted on <see cref="UserProfile.QrId"/>; the
/// uniqueness query is a LINQ-IQueryable read against
/// <c>SimfAppDbContext.UserProfiles</c>, which is where the entity
/// lives.</para>
/// </summary>
internal sealed class QrIdMinter(SimfAppDbContext dbContext) : IQrIdMinter
{
    private const int MaxAttempts = 8;

    public async Task<string> MintIfMissingAsync(
        UserProfile profile, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(profile.QrId))
        {
            return profile.QrId;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = QrIdCandidate.Generate();
            var clash = await dbContext.UserProfiles
                .AsNoTracking()
                .AnyAsync(p => p.QrId == candidate, cancellationToken);
            if (!clash)
            {
                profile.QrId = candidate;
                return candidate;
            }
        }
        throw new InvalidOperationException(
            "Could not mint a unique QR id after several attempts — exhaustion is implausible at this scale.");
    }

}
