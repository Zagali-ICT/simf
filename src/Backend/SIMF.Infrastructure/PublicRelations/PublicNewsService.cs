// Tests: SIMF.Api.Tests/NewsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Contracts.PublicRelations;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>D-199 (Mockup screen 29 / 29b) — public read of the News feed.
/// Surfaces only active articles whose <c>PublishedAt</c> has passed, newest
/// first. Built on <see cref="SimfAppDbContext"/>; mirrors
/// <c>PublicDelegationService</c> but adds paging because a news feed grows
/// unbounded.</summary>
internal sealed class PublicNewsService(
    SimfAppDbContext dbContext,
    TimeProvider timeProvider) : IPublicNewsService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    public async Task<PublicNewsPage> ListAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) { page = 1; }
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var now = timeProvider.SimfNow();

        var visible = dbContext.News
            .AsNoTracking()
            .Where(news => news.IsActive && news.PublishedAt <= now);

        var total = await visible.CountAsync(cancellationToken);

        var items = await visible
            .OrderByDescending(news => news.PublishedAt)
            .ThenBy(news => news.DisplayOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(news => new PublicNewsListItem(
                news.Id,
                news.Title,
                news.TitleArabic,
                news.Excerpt,
                news.ExcerptArabic,
                news.Category,
                news.CategoryArabic,
                news.ImageRelativePath,
                news.PublishedAt))
            .ToListAsync(cancellationToken);

        return new PublicNewsPage(items, total, page, pageSize);
    }

    public async Task<PublicNewsArticle?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();

        return await dbContext.News
            .AsNoTracking()
            .Where(news => news.Id == id && news.IsActive && news.PublishedAt <= now)
            .Select(news => new PublicNewsArticle(
                news.Id,
                news.Title,
                news.TitleArabic,
                news.Body,
                news.BodyArabic,
                news.Category,
                news.CategoryArabic,
                news.ImageRelativePath,
                news.PublishedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
