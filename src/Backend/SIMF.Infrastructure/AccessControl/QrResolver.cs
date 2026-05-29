// Tests: SIMF.Api.Tests/Gates/GateScanTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>D-148 — implements <see cref="IQrResolver"/> against the existing
/// <c>UserProfile</c> + <c>SimfUser</c> + <c>ProfileType</c> tables. Single
/// projected query, no tracking. The gate engine calls this in step 3.</summary>
internal sealed class QrResolver(SimfIdentityDbContext identityDbContext)
    : IQrResolver
{
    public async Task<QrResolution?> ResolveAsync(
        string qrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }
        var normalised = qrId.Trim().ToUpperInvariant();

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
                row.user.LockoutEnd != null && row.user.LockoutEnd > DateTimeOffset.UtcNow,
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
