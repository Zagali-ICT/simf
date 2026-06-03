// Tests: SIMF.Api.Tests/NewsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>D-199 — admin CRUD over <see cref="News"/> (PR / marketing).
/// Mirrors <see cref="SIMF.Infrastructure.Delegations.AdminDelegationService"/>:
/// built on <see cref="SimfAppDbContext"/>, writes one audit row per mutation,
/// stamps timestamps via <see cref="TimeProvider"/>, and guards a unique
/// English title with a 409. The admin list returns every row (including
/// soft-deleted / not-yet-published) so editors can manage drafts.</summary>
internal sealed class AdminNewsService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminNewsService> logger) : IAdminNewsService
{
    private const int TitleMaxLength = 200;
    private const int ExcerptMaxLength = 500;
    private const int BodyMaxLength = 8000;
    private const int CategoryMaxLength = 100;
    private const int ImagePathMaxLength = 512;

    public async Task<GridPage<AdminNewsSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.News.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(news =>
                EF.Functions.Like(news.TitleEn, $"%{term}%")
                || EF.Functions.Like(news.TitleAr, $"%{term}%")
                || EF.Functions.Like(news.CategoryEn, $"%{term}%")
                || EF.Functions.Like(news.CategoryAr, $"%{term}%"));
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        // The column keys match the SimfDataGridColumn `Key` values on the page.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "titleen":
                    rows = rows.Where(news => news.TitleEn.Contains(v));
                    break;
                case "titlear":
                    rows = rows.Where(news => news.TitleAr.Contains(v));
                    break;
                case "categoryen":
                    rows = rows.Where(news => news.CategoryEn.Contains(v));
                    break;
                case "categoryar":
                    rows = rows.Where(news => news.CategoryAr.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(news => news.IsActive == isActive);
                    }
                    break;
            }
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("titleen", true) => rows.OrderByDescending(news => news.TitleEn),
            ("titleen", false) => rows.OrderBy(news => news.TitleEn),
            ("displayorder", true) => rows.OrderByDescending(news => news.DisplayOrder),
            ("displayorder", false) => rows.OrderBy(news => news.DisplayOrder),
            ("publishedat", false) => rows.OrderBy(news => news.PublishedAt),
            _ => rows.OrderByDescending(news => news.PublishedAt)
                     .ThenBy(news => news.DisplayOrder),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(news => new AdminNewsSummary(
                news.Id,
                news.TitleEn,
                news.TitleAr,
                news.CategoryEn,
                news.CategoryAr,
                news.PublishedAt,
                news.DisplayOrder,
                news.IsActive,
                news.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminNewsSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
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
            request.TitleEn, request.TitleAr,
            request.ExcerptEn, request.ExcerptAr,
            request.BodyEn, request.BodyAr,
            request.CategoryEn, request.CategoryAr,
            request.ImageRelativePath, request.DisplayOrder);

        var clash = await dbContext.News
            .AsNoTracking()
            .AnyAsync(news => news.TitleEn == draft.TitleEn, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.NewsTitleDuplicate, 409,
                $"A news article with the English title '{draft.TitleEn}' already exists.",
                $"يوجد خبر بالعنوان الإنجليزي '{draft.TitleEn}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var news = new News
        {
            Id = Guid.NewGuid(),
            TitleEn = draft.TitleEn,
            TitleAr = draft.TitleAr,
            ExcerptEn = draft.ExcerptEn,
            ExcerptAr = draft.ExcerptAr,
            BodyEn = draft.BodyEn,
            BodyAr = draft.BodyAr,
            CategoryEn = draft.CategoryEn,
            CategoryAr = draft.CategoryAr,
            ImageRelativePath = draft.ImageRelativePath,
            PublishedAt = request.PublishedAt,
            DisplayOrder = draft.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.News.Add(news);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.NewsCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={news.Id}; titleEn={news.TitleEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created News {TitleEn} ({Id})",
            actorUserId, news.TitleEn, news.Id);

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
            request.TitleEn, request.TitleAr,
            request.ExcerptEn, request.ExcerptAr,
            request.BodyEn, request.BodyAr,
            request.CategoryEn, request.CategoryAr,
            request.ImageRelativePath, request.DisplayOrder);

        if (!string.Equals(news.TitleEn, draft.TitleEn, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.News
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.TitleEn == draft.TitleEn, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.NewsTitleDuplicate, 409,
                    $"A news article with the English title '{draft.TitleEn}' already exists.",
                    $"يوجد خبر بالعنوان الإنجليزي '{draft.TitleEn}' بالفعل.");
            }
        }

        news.TitleEn = draft.TitleEn;
        news.TitleAr = draft.TitleAr;
        news.ExcerptEn = draft.ExcerptEn;
        news.ExcerptAr = draft.ExcerptAr;
        news.BodyEn = draft.BodyEn;
        news.BodyAr = draft.BodyAr;
        news.CategoryEn = draft.CategoryEn;
        news.CategoryAr = draft.CategoryAr;
        news.ImageRelativePath = draft.ImageRelativePath;
        news.PublishedAt = request.PublishedAt;
        news.DisplayOrder = draft.DisplayOrder;
        news.IsActive = request.IsActive;
        news.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.NewsUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={news.Id}; titleEn={news.TitleEn}; active={news.IsActive}",
        }, cancellationToken);

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
        news.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.NewsDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={news.Id}; titleEn={news.TitleEn}",
        }, cancellationToken);
    }

    private sealed record NewsDraft(
        string TitleEn, string TitleAr,
        string? ExcerptEn, string? ExcerptAr,
        string BodyEn, string BodyAr,
        string CategoryEn, string CategoryAr,
        string? ImageRelativePath, int DisplayOrder);

    private static NewsDraft Validate(
        string? titleEnRaw, string? titleArRaw,
        string? excerptEnRaw, string? excerptArRaw,
        string? bodyEnRaw, string? bodyArRaw,
        string? categoryEnRaw, string? categoryArRaw,
        string? imagePathRaw, int displayOrderRaw)
    {
        var titleEn = RequireText(titleEnRaw, TitleMaxLength, "English title", "العنوان الإنجليزي");
        var titleAr = RequireText(titleArRaw, TitleMaxLength, "Arabic title", "العنوان العربي");
        var bodyEn = RequireText(bodyEnRaw, BodyMaxLength, "English body", "النص الإنجليزي");
        var bodyAr = RequireText(bodyArRaw, BodyMaxLength, "Arabic body", "النص العربي");
        var categoryEn = RequireText(categoryEnRaw, CategoryMaxLength, "English category", "التصنيف الإنجليزي");
        var categoryAr = RequireText(categoryArRaw, CategoryMaxLength, "Arabic category", "التصنيف العربي");

        var excerptEn = OptionalText(excerptEnRaw, ExcerptMaxLength, "English excerpt", "المقتطف الإنجليزي");
        var excerptAr = OptionalText(excerptArRaw, ExcerptMaxLength, "Arabic excerpt", "المقتطف العربي");
        var imagePath = OptionalText(imagePathRaw, ImagePathMaxLength, "image path", "مسار الصورة");

        if (displayOrderRaw < 0)
        {
            throw new ApiException(
                ErrorCodes.NewsInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }

        return new NewsDraft(
            titleEn, titleAr, excerptEn, excerptAr,
            bodyEn, bodyAr, categoryEn, categoryAr, imagePath, displayOrderRaw);
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
            news.TitleEn, news.TitleAr,
            news.ExcerptEn, news.ExcerptAr,
            news.BodyEn, news.BodyAr,
            news.CategoryEn, news.CategoryAr,
            news.ImageRelativePath,
            news.PublishedAt, news.DisplayOrder, news.IsActive,
            news.CreatedAt, news.UpdatedAt);
}
