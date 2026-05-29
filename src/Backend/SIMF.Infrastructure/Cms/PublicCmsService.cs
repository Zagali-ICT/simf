// Tests: SIMF.Api.Tests/CmsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Cms.Abstractions;
using SIMF.Contracts.Cms;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Cms;

/// <summary>
/// D-173 (gap doc G8) — public read surface. Hot path on every public
/// page render — kept minimal: AsNoTracking, projection straight to
/// the contract record, single round trip even on the batch path.
/// </summary>
internal sealed class PublicCmsService(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider) : IPublicCmsService
{
    public async Task<PublicContentBlock?> GetContentBlockAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalised = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalised))
        {
            return null;
        }
        return await appDbContext.ContentBlocks
            .AsNoTracking()
            .Where(b => b.Key == normalised && b.IsActive)
            .Select(b => new PublicContentBlock(
                b.Key, b.ContentEn, b.ContentAr, b.LastUpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PublicContentBlockBatch> GetContentBlocksAsync(
        IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return new PublicContentBlockBatch(
                new Dictionary<string, PublicContentBlock>(0));
        }
        var normalised = keys
            .Select(k => (k ?? string.Empty).Trim().ToLowerInvariant())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList();
        var rows = await appDbContext.ContentBlocks
            .AsNoTracking()
            .Where(b => normalised.Contains(b.Key) && b.IsActive)
            .Select(b => new PublicContentBlock(
                b.Key, b.ContentEn, b.ContentAr, b.LastUpdatedAt))
            .ToListAsync(cancellationToken);
        return new PublicContentBlockBatch(
            rows.ToDictionary(r => r.Key));
    }

    public async Task<PublicBanners> GetActiveBannersAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await appDbContext.Banners
            .AsNoTracking()
            .Where(b => b.IsActive && b.StartUtc <= now && b.EndUtc >= now)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.StartUtc)
            .Select(b => new PublicBanner(
                b.Id, b.TitleEn, b.TitleAr, b.BodyEn, b.BodyAr,
                b.ImageUrl, b.LinkUrl, b.StartUtc, b.EndUtc, b.DisplayOrder))
            .ToListAsync(cancellationToken);
        return new PublicBanners(rows);
    }
}
