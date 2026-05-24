// Tests: SIMF.Api.Tests/UserProfileTests.cs (QR minted on admin-create-user;
//        not minted before Approved).
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Crockford base32 12-character QR-id minter (decision D-046). 32^12 ≈
/// 1.2 × 10^18 possible values; collisions are negligible at SIMF scale
/// (thousands of users), but we still query the DB once to ensure
/// uniqueness — the column carries a UNIQUE constraint anyway.
/// </summary>
internal sealed class QrIdMinter(UserManager<SimfUser> userManager) : IQrIdMinter
{
    /// <summary>Crockford base32 alphabet — excludes I, L, O, U and 0/1.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 12;
    private const int MaxAttempts = 8;

    public async Task<string> MintIfMissingAsync(
        SimfUser user, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(user.QrId))
        {
            return user.QrId;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = Generate();
            var clash = await userManager.Users
                .AsNoTracking()
                .AnyAsync(u => u.QrId == candidate, cancellationToken);
            if (!clash)
            {
                user.QrId = candidate;
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
