// Tests: SIMF.Api.Tests/GateScanTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>Implements <see cref="IQrResolver"/> against
/// <c>UserProfile</c> + <c>SimfUser</c> + <c>ProfileType</c>. Single projected
/// query, no tracking.</summary>
internal sealed class QrResolver(
    SimfIdentityDbContext identityDbContext,
    TimeProvider timeProvider) : IQrResolver
{
    public async Task<QrResolution?> ResolveAsync(
        string qrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }
        var normalised = QrId.Normalise(qrId);
        var now = timeProvider.GetUtcNow();

        return await identityDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.QrId == normalised)
            .Join(
                identityDbContext.Users.AsNoTracking(),
                profile => profile.UserId,
                user => user.Id,
                (profile, user) => new { profile, user })
            .Select(row => new QrResolution(
                row.profile.Id,
                row.user.Id,
                row.user.AccountState,
                row.user.LockoutEnd != null && row.user.LockoutEnd > now,
                row.profile.ProfileTypeId,
                row.profile.ProfileType != null && row.profile.ProfileType.IsActive,
                row.profile.ProfileType != null ? row.profile.ProfileType.Name : null,
                row.profile.ProfileType != null ? row.profile.ProfileType.NameArabic : null,
                row.profile.ProfileType != null ? row.profile.ProfileType.PageColor : null,
                row.user.DisplayName ?? string.Empty,
                row.profile.ArabicName))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

/// <summary>Canonical form of a QR id. Trim + upper-case; the QR is
/// case-insensitive on every scan path.</summary>
internal static class QrId
{
    public static string Normalise(string raw) =>
        (raw ?? string.Empty).Trim().ToUpperInvariant();
}
