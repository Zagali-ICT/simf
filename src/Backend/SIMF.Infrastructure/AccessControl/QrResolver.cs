// Tests: SIMF.Api.Tests/GateScanTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>Implements <see cref="IQrResolver"/> against
/// <c>UserProfile</c> + <c>SimfUser</c> + <c>ProfileType</c>. After D-167
/// moved UserProfile + ProfileType onto SimfAppDbContext, this is two
/// round-trips: App-DB lookup by QR id, then Identity-DB lookup by user
/// id. Both queries are PK / unique-index hits so total latency stays at
/// sub-millisecond.</summary>
internal sealed class QrResolver(
    SimfIdentityDbContext identityDbContext,
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider) : IQrResolver
{
    public async Task<QrResolution?> ResolveAsync(
        string qrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }
        var normalised = QrId.Normalise(qrId);
        var now = timeProvider.GetUtcNow();

        var profileRow = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.QrId == normalised)
            .Select(profile => new
            {
                profile.Id,
                profile.UserId,
                profile.ProfileTypeId,
                profileTypeActive = profile.ProfileType != null && profile.ProfileType.IsActive,
                profileTypeName = profile.ProfileType != null ? profile.ProfileType.Name : null,
                profileTypeNameAr = profile.ProfileType != null ? profile.ProfileType.NameArabic : null,
                profileTypePageColor = profile.ProfileType != null ? profile.ProfileType.PageColor : null,
                profile.NameArabic,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (profileRow is null) { return null; }

        var userRow = await identityDbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == profileRow.UserId)
            .Select(user => new
            {
                user.Id,
                user.AccountState,
                user.LockoutEnd,
                user.DisplayName,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (userRow is null) { return null; }

        return new QrResolution(
            profileRow.Id,
            userRow.Id,
            userRow.AccountState,
            userRow.LockoutEnd != null && userRow.LockoutEnd > now,
            profileRow.ProfileTypeId,
            profileRow.profileTypeActive,
            profileRow.profileTypeName,
            profileRow.profileTypeNameAr,
            profileRow.profileTypePageColor,
            userRow.DisplayName ?? string.Empty,
            profileRow.ArabicName);
    }
}

/// <summary>Canonical form of a QR id. Trim + upper-case; the QR is
/// case-insensitive on every scan path.</summary>
internal static class QrId
{
    public static string Normalise(string raw) =>
        (raw ?? string.Empty).Trim().ToUpperInvariant();
}
