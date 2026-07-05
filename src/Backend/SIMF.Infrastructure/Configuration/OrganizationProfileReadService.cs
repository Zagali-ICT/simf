// Tests: SIMF.Api.Tests/OrganizationProfileTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>D-495 — the public, IMemoryCache-backed read of the singleton
/// Organization Profile (mirrors <c>GateConfigCache</c>). 5-minute TTL plus explicit
/// <see cref="Invalidate"/> on every admin write, so an edit shows immediately while a
/// stale read can never persist past the TTL. The snapshot carries the row's
/// last-write instant — the endpoint's <c>Last-Modified</c> / 304 token.</summary>
internal sealed class OrganizationProfileReadService(
    IMemoryCache cache,
    SimfAppDbContext db) : IOrganizationProfileReadService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const string CacheKey = "org-profile:v1";

    public async Task<OrganizationProfileSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<OrganizationProfileSnapshot>(CacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        var snapshot = await LoadAsync(cancellationToken);
        cache.Set(CacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
        });
        return snapshot;
    }

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<OrganizationProfileSnapshot> LoadAsync(CancellationToken ct)
    {
        var profile = await db.OrganizationProfile.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, ct);

        if (profile is null)
        {
            // The singleton is seeded, so this only happens on an un-migrated DB —
            // answer 200 with an empty profile rather than fail the public read.
            var empty = OrganizationProfileMapper.ToResponse(
                new OrganizationProfile(), [], [], null);
            return new OrganizationProfileSnapshot(empty, DateTimeOffset.UnixEpoch);
        }

        var about = await db.OrganizationAboutItems.AsNoTracking()
            .Where(a => a.OrganizationProfileId == OrganizationProfile.SingletonId && a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync(ct);

        var details = await db.OrganizationDetails.AsNoTracking()
            .Where(d => d.OrganizationProfileId == OrganizationProfile.SingletonId && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync(ct);

        var hasLogo = await db.StoredFiles.AsNoTracking().AnyAsync(
            f => f.IsActive
                && f.Service == FileService.OrganizationLogo
                && f.OwnerEntityId == OrganizationProfile.SingletonId, ct);

        var logoUrl = hasLogo
            ? $"app/assets/{AssetCategory.OrganizationLogo}/{OrganizationProfile.SingletonId}/image"
            : null;

        var response = OrganizationProfileMapper.ToResponse(profile, about, details, logoUrl);
        var lastModified = profile.UpdatedAt ?? profile.CreatedAt;
        return new OrganizationProfileSnapshot(response, lastModified);
    }
}
