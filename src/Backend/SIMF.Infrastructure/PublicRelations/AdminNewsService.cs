// Tests: SIMF.Api.Tests/NewsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>Admin CRUD over <see cref="News"/> (PR / marketing).
/// Mirrors <c>AdminDelegationService</c>:
/// built on <see cref="SimfAppDbContext"/>, writes one audit row per mutation,
/// stamps timestamps via <see cref="TimeProvider"/>, and guards a unique
/// English title with a 409. The admin list returns every row (including
/// soft-deleted / not-yet-published) so editors can manage drafts.</summary>
internal sealed class AdminNewsService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IAssetService assetService,
    ILogger<AdminNewsService> logger) : IAdminNewsService
{
    private const int TitleMaxLength = 200;
    private const int ExcerptMaxLength = 500;
    private const int BodyMaxLength = 8000;
    private const int CategoryMaxLength = 100;

    /// <summary>
    /// The grid contract for /admin/news: one entry per key NewsList.razor can send,
    /// as both its filter and its sort. A key not declared here is a 400, not a
    /// silently ignored request.
    /// </summary>
    private static readonly GridColumns<News> Columns = new GridColumns<News>()
        .Add("title", news => news.Title, searchable: true)
        .Add("titleArabic", news => news.TitleArabic, searchable: true)
        .Add("category", news => news.Category, searchable: true)
        .Add("categoryArabic", news => news.CategoryArabic, searchable: true)
        .Add("publishedAt", news => news.PublishedAt)
        .Add("displayOrder", news => news.DisplayOrder)
        .Add("isActive", news => news.IsActive)
        .DefaultOrder("publishedAt", descending: true)
        .DefaultOrder("displayOrder")
        .PageSize(fallback: 25, max: 200);

    // HasImage is projected false and filled in below: "an active NewsImage asset
    // exists" is a file-store fact, not a column on News, so it cannot be part of
    // the SELECT.
    private static readonly Expression<Func<News, AdminNewsSummary>> ToSummary =
        news => new AdminNewsSummary(
            news.Id,
            news.Title,
            news.TitleArabic,
            news.Category,
            news.CategoryArabic,
            news.PublishedAt,
            news.DisplayOrder,
            news.IsActive,
            false,
            news.CreatedAt,
            // Append in the same positional order as the record so the
            // Excel export round-trips the bilingual body + excerpt.
            news.BodyArabic,
            news.ExcerptArabic);

    public async Task<GridPage<AdminNewsSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var page = await dbContext.News.ToGridPageAsync(
            query, Columns, news => news.Id, ToSummary, cancellationToken);

        // The grid renders the news image thumbnail only when an active NewsImage
        // asset exists (StoredFile store via the /assets proxy, not the legacy
        // ImageRelativePath) — one batched query for the page, no N+1.
        var imageOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.NewsImage,
            page.Items.Select(news => news.Id).ToList(),
            cancellationToken);

        return GridPage<AdminNewsSummary>.Of(
            page.Items
                .Select(news => news with { HasImage = imageOwners.Contains(news.Id) })
                .ToList(),
            page.Total,
            page.Skip,
            page.Top);
    }

    public async Task<AdminNewsDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.News
            .AsNoTracking()
            .Where(news => news.Id == id)
            .Select(news => ToDetail(news))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminNewsDetail> CreateAsync(
        Guid actorUserId,
        CreateNewsRequest request,
        CancellationToken cancellationToken = default)
    {
        var draft = Validate(
            request.Title, request.TitleArabic,
            request.Excerpt, request.ExcerptArabic,
            request.Body, request.BodyArabic,
            request.Category, request.CategoryArabic,
            request.DisplayOrder);

        var clash = await dbContext.News
            .AsNoTracking()
            .AnyAsync(news => news.Title == draft.Title, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.NewsTitleDuplicate, 409,
                $"A news article with the English title '{draft.Title}' already exists.",
                $"يوجد خبر بالعنوان الإنجليزي '{draft.Title}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var news = new News
        {
            Id = Guid.NewGuid(),
            Title = draft.Title,
            TitleArabic = draft.TitleArabic,
            Excerpt = draft.Excerpt,
            ExcerptArabic = draft.ExcerptArabic,
            Body = draft.Body,
            BodyArabic = draft.BodyArabic,
            Category = draft.Category,
            CategoryArabic = draft.CategoryArabic,
            PublishedAt = request.PublishedAt,
            DisplayOrder = draft.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.News.Add(news);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.NewsCreated,
            actorUserId,
            $"id={news.Id}; title={news.Title}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created News {Title} ({Id})",
            actorUserId, news.Title, news.Id);

        return ToDetail(news);
    }

    public async Task<AdminNewsDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateNewsRequest request,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.NewsNotFound, 404,
                "The news article was not found.",
                "لم يتم العثور على الخبر.");

        var draft = Validate(
            request.Title, request.TitleArabic,
            request.Excerpt, request.ExcerptArabic,
            request.Body, request.BodyArabic,
            request.Category, request.CategoryArabic,
            request.DisplayOrder);

        if (!string.Equals(news.Title, draft.Title, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.News
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Title == draft.Title, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.NewsTitleDuplicate, 409,
                    $"A news article with the English title '{draft.Title}' already exists.",
                    $"يوجد خبر بالعنوان الإنجليزي '{draft.Title}' بالفعل.");
            }
        }

        news.Title = draft.Title;
        news.TitleArabic = draft.TitleArabic;
        news.Excerpt = draft.Excerpt;
        news.ExcerptArabic = draft.ExcerptArabic;
        news.Body = draft.Body;
        news.BodyArabic = draft.BodyArabic;
        news.Category = draft.Category;
        news.CategoryArabic = draft.CategoryArabic;
        news.PublishedAt = request.PublishedAt;
        news.DisplayOrder = draft.DisplayOrder;
        news.IsActive = request.IsActive;
        news.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.NewsUpdated,
            actorUserId,
            $"id={news.Id}; title={news.Title}; active={news.IsActive}",
            cancellationToken);

        return ToDetail(news);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.NewsNotFound, 404,
                "The news article was not found.",
                "لم يتم العثور على الخبر.");

        if (!news.IsActive) { return; } // idempotent

        news.IsActive = false;
        news.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.NewsDeactivated,
            actorUserId,
            $"id={news.Id}; title={news.Title}",
            cancellationToken);
    }

    private sealed record NewsDraft(
        string Title, string TitleArabic,
        string? Excerpt, string? ExcerptArabic,
        string Body, string BodyArabic,
        string Category, string CategoryArabic,
        int DisplayOrder);

    private static NewsDraft Validate(
        string? titleRaw, string? titleArabicRaw,
        string? excerptRaw, string? excerptArabicRaw,
        string? bodyRaw, string? bodyArabicRaw,
        string? categoryRaw, string? categoryArabicRaw,
        int displayOrderRaw)
    {
        var title = RequireText(titleRaw, TitleMaxLength, "English title", "العنوان الإنجليزي");
        var titleArabic = RequireText(titleArabicRaw, TitleMaxLength, "Arabic title", "العنوان العربي");
        var body = RequireText(bodyRaw, BodyMaxLength, "English body", "النص الإنجليزي");
        var bodyArabic = RequireText(bodyArabicRaw, BodyMaxLength, "Arabic body", "النص العربي");
        var category = RequireText(categoryRaw, CategoryMaxLength, "English category", "التصنيف الإنجليزي");
        var categoryArabic = RequireText(categoryArabicRaw, CategoryMaxLength, "Arabic category", "التصنيف العربي");

        var excerpt = OptionalText(excerptRaw, ExcerptMaxLength, "English excerpt", "المقتطف الإنجليزي");
        var excerptArabic = OptionalText(excerptArabicRaw, ExcerptMaxLength, "Arabic excerpt", "المقتطف العربي");

        if (displayOrderRaw < 0)
        {
            throw new ApiException(
                ErrorCodes.NewsInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }

        return new NewsDraft(
            title, titleArabic, excerpt, excerptArabic,
            body, bodyArabic, category, categoryArabic, displayOrderRaw);
    }

    private static string RequireText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length < 1)
        {
            throw new ApiException(
                ErrorCodes.NewsInvalid, 400,
                $"News {fieldEn} is required.",
                $"{fieldAr} مطلوب.");
        }
        if (value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.NewsInvalid, 400,
                $"News {fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    private static string? OptionalText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return null; }
        var value = raw.Trim();
        if (value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.NewsInvalid, 400,
                $"News {fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    private static AdminNewsDetail ToDetail(News news) =>
        new(news.Id,
            news.Title, news.TitleArabic,
            news.Excerpt, news.ExcerptArabic,
            news.Body, news.BodyArabic,
            news.Category, news.CategoryArabic,
            null,
            news.PublishedAt, news.DisplayOrder, news.IsActive,
            news.CreatedAt, news.UpdatedAt);
}
