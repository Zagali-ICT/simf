// Tests: SIMF.Api.Tests/UserProfileTests.cs (QR minted on admin-create-user;
//        not minted before Approved).
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Crockford base32 12-character QR-id minter (decision D-046). 32^12 ≈
/// 1.2 × 10^18 possible values; collisions are negligible at SIMF scale
/// (thousands of users), but we still query the DB once to ensure
/// uniqueness — the column carries a UNIQUE constraint anyway.
///
/// <para>D-106: minted on <see cref="UserProfile.QrId"/> (was
/// <see cref="SimfUser.QrId"/> pre-D-106). The uniqueness query is a
/// LINQ-IQueryable read against <c>SimfIdentityDbContext.UserProfiles</c>
/// — using the DbContext directly here is the Infrastructure-only seam
/// that avoids leaking <c>IQueryable&lt;UserProfile&gt;</c> out of the
/// repository contract.</para>
/// </summary>
internal sealed class QrIdMinter(SimfIdentityDbContext dbContext) : IQrIdMinter
{
    /// <summary>Crockford base32 alphabet — excludes I, L, O, U and 0/1.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 12;
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
            var candidate = Generate();
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

    private static string Generate()
    {
        Span<char> buffer = stackalloc char[Length];
        Span<byte> entropy = stackalloc byte[Length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(entropy);
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[entropy[i] % Alphabet.Length];
        }
        return new string(buffer);
    }
}
