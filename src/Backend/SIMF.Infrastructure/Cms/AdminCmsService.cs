// Tests: SIMF.Api.Tests/CmsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Cms.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Cms;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Cms;

/// <summary>
/// D-173 (gap doc G8, PDF §1, §2.1) — admin CRUD over content blocks
/// and banners. Both entities live on the App DB; logical FK on
/// <c>LastUpdatedByUserId</c> to <c>SimfUser</c> on the Identity DB.
/// </summary>
internal sealed class AdminCmsService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminCmsService> logger) : IAdminCmsService
{
    public async Task<GridPage<AdminContentBlockSummary>> ListContentBlocksAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.ContentBlocks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(b =>
                EF.Functions.Like(b.Key, $"%{term}%")
                || EF.Functions.Like(b.Content, $"%{term}%")
                || EF.Functions.Like(b.ContentArabic, $"%{term}%"));
        }

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "key":
                    rows = rows.Where(b => b.Key.Contains(v));
                    break;
                case "content":
                    rows = rows.Where(b => b.Content.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(b => b.IsActive == isActive);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: Key ascending.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("key", true) => rows.OrderByDescending(b => b.Key),
            ("key", false) => rows.OrderBy(b => b.Key),
            ("content", true) => rows.OrderByDescending(b => b.Content),
            ("content", false) => rows.OrderBy(b => b.Content),
            ("lastupdatedat", true) => rows.OrderByDescending(b => b.LastUpdatedAt),
            ("lastupdatedat", false) => rows.OrderBy(b => b.LastUpdatedAt),
            _ => rows.OrderBy(b => b.Key),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(b => new AdminContentBlockSummary(
                b.Id, b.Key, b.Content, b.ContentArabic, b.IsActive,
                b.LastUpdatedAt, b.LastUpdatedByUserId))
            .ToListAsync(cancellationToken);

        return GridPage<AdminContentBlockSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminContentBlockSummary?> GetContentBlockAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalised = NormaliseKey(key);
        var row = await appDbContext.ContentBlocks
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Key == normalised, cancellationToken);
        return row is null
            ? null
            : new AdminContentBlockSummary(
                row.Id, row.Key, row.Content, row.ContentArabic, row.IsActive,
                row.LastUpdatedAt, row.LastUpdatedByUserId);
    }

    public async Task<AdminContentBlockSummary> UpsertContentBlockAsync(
        Guid actorUserId,
        UpsertContentBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = NormaliseKey(request.Key);
        var content = (request.Content ?? string.Empty);
        var contentArabic = (request.ContentArabic ?? string.Empty);

        if (key.Length is < 2 or > 128)
        {
            throw new ApiException(
                ErrorCodes.ContentBlockInvalid, 400,
                "Content block key must be between 2 and 128 characters.",
                "يجب أن يتراوح طول مفتاح المحتوى بين 2 و 128 حرفاً.");
        }
        if (content.Length > 8000 || contentArabic.Length > 8000)
        {
            throw new ApiException(
                ErrorCodes.ContentBlockInvalid, 400,
                "Content cannot exceed 8000 characters.",
                "لا يمكن أن يتجاوز المحتوى 8000 حرف.");
        }

        var now = timeProvider.GetUtcNow();
        var existing = await appDbContext.ContentBlocks
            .SingleOrDefaultAsync(b => b.Key == key, cancellationToken);

        if (existing is null)
        {
            existing = new ContentBlock
            {
                Id = Guid.NewGuid(),
                Key = key,
                Content = content,
                ContentArabic = contentArabic,
                IsActive = request.IsActive,
                CreatedAt = now,
                LastUpdatedAt = now,
                LastUpdatedByUserId = actorUserId,
            };
            appDbContext.ContentBlocks.Add(existing);
        }
        else
        {
            existing.Content = content;
            existing.ContentArabic = contentArabic;
            existing.IsActive = request.IsActive;
            existing.LastUpdatedAt = now;
            existing.LastUpdatedByUserId = actorUserId;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ContentBlockUpserted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"key={key}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {Actor} upserted content block {Key}", actorUserId, key);

        return new AdminContentBlockSummary(
            existing.Id, existing.Key, existing.Content, existing.ContentArabic,
            existing.IsActive, existing.LastUpdatedAt, existing.LastUpdatedByUserId);
    }

    public async Task DeactivateContentBlockAsync(
        Guid actorUserId, string key, CancellationToken cancellationToken = default)
    {
        var normalised = NormaliseKey(key);
        var existing = await appDbContext.ContentBlocks
            .SingleOrDefaultAsync(b => b.Key == normalised, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ContentBlockNotFound, 404,
                "Content block not found.",
                "لم يتم العثور على المحتوى.");

        if (!existing.IsActive)
        {
            return; // idempotent
        }
        existing.IsActive = false;
        existing.LastUpdatedAt = timeProvider.GetUtcNow();
        existing.LastUpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ContentBlockDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"key={normalised}",
        }, cancellationToken);
    }

    public async Task<GridPage<AdminBannerSummary>> ListBannersAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.Banners.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(b =>
                EF.Functions.Like(b.Title, $"%{term}%")
                || EF.Functions.Like(b.TitleArabic, $"%{term}%"));
        }
        // CP grid per-column filters (D-256). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "title":
                    rows = rows.Where(b => b.Title.Contains(v) || b.TitleArabic.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(b => b.IsActive == isActive);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-256). Default: DisplayOrder, then StartUtc.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("title", false) => rows.OrderBy(b => b.Title),
            ("title", true) => rows.OrderByDescending(b => b.Title),
            ("startutc", false) => rows.OrderBy(b => b.StartUtc),
            ("startutc", true) => rows.OrderByDescending(b => b.StartUtc),
            ("endutc", false) => rows.OrderBy(b => b.EndUtc),
            ("endutc", true) => rows.OrderByDescending(b => b.EndUtc),
            ("displayorder", true) => rows.OrderByDescending(b => b.DisplayOrder).ThenBy(b => b.StartUtc),
            ("isactive", false) => rows.OrderBy(b => b.IsActive),
            ("isactive", true) => rows.OrderByDescending(b => b.IsActive),
            _ => rows.OrderBy(b => b.DisplayOrder).ThenBy(b => b.StartUtc),
        };
        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(b => new AdminBannerSummary(
                b.Id, b.Title, b.TitleArabic,
                b.StartUtc, b.EndUtc, b.DisplayOrder, b.IsActive, b.CreatedAt,
                // D-506 — round-trip body + image/link through the grid Excel export.
                b.Body, b.BodyArabic, b.ImageUrl, b.LinkUrl))
            .ToListAsync(cancellationToken);

        return GridPage<AdminBannerSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminBannerDetail?> GetBannerAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.Banners
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        return row is null ? null : ToBannerDetail(row);
    }

    public async Task<AdminBannerDetail> CreateBannerAsync(
        Guid actorUserId,
        CreateBannerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBanner(request.Title, request.TitleArabic,
            request.Body, request.BodyArabic, request.StartUtc, request.EndUtc,
            request.DisplayOrder);

        var now = timeProvider.GetUtcNow();
        var banner = new Banner
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            TitleArabic = request.TitleArabic.Trim(),
            Body = request.Body.Trim(),
            BodyArabic = request.BodyArabic.Trim(),
            ImageUrl = NullIfBlank(request.ImageUrl),
            LinkUrl = NullIfBlank(request.LinkUrl),
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Banners.Add(banner);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BannerCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"bannerId={banner.Id}",
        }, cancellationToken);

        return ToBannerDetail(banner);
    }

    public async Task<AdminBannerDetail> UpdateBannerAsync(
        Guid actorUserId, Guid id, UpdateBannerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBanner(request.Title, request.TitleArabic,
            request.Body, request.BodyArabic, request.StartUtc, request.EndUtc,
            request.DisplayOrder);

        var banner = await appDbContext.Banners
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BannerNotFound, 404,
                "Banner not found.",
                "لم يتم العثور على البانر.");

        banner.Title = request.Title.Trim();
        banner.TitleArabic = request.TitleArabic.Trim();
        banner.Body = request.Body.Trim();
        banner.BodyArabic = request.BodyArabic.Trim();
        banner.ImageUrl = NullIfBlank(request.ImageUrl);
        banner.LinkUrl = NullIfBlank(request.LinkUrl);
        banner.StartUtc = request.StartUtc;
        banner.EndUtc = request.EndUtc;
        banner.DisplayOrder = request.DisplayOrder;
        banner.IsActive = request.IsActive;
        banner.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BannerUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"bannerId={banner.Id}; active={banner.IsActive}",
        }, cancellationToken);

        return ToBannerDetail(banner);
    }

    public async Task DeactivateBannerAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var banner = await appDbContext.Banners
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BannerNotFound, 404,
                "Banner not found.",
                "لم يتم العثور على البانر.");

        if (!banner.IsActive)
        {
            return; // idempotent
        }
        banner.IsActive = false;
        banner.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BannerDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"bannerId={banner.Id}",
        }, cancellationToken);
    }

    // -- helpers --------------------------------------------------------------

    private static string NormaliseKey(string raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateBanner(
        string title, string titleArabic, string body, string bodyArabic,
        DateTimeOffset startUtc, DateTimeOffset endUtc, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256
            || string.IsNullOrWhiteSpace(titleArabic) || titleArabic.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.BannerInvalid, 400,
                "Banner title (EN + AR) must be between 1 and 256 characters.",
                "يجب أن يتراوح طول العنوان (إنجليزي + عربي) بين 1 و 256 حرفاً.");
        }
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000
            || string.IsNullOrWhiteSpace(bodyArabic) || bodyArabic.Length > 2000)
        {
            throw new ApiException(
                ErrorCodes.BannerInvalid, 400,
                "Banner body (EN + AR) must be between 1 and 2000 characters.",
                "يجب أن يتراوح طول النص (إنجليزي + عربي) بين 1 و 2000 حرف.");
        }
        if (endUtc <= startUtc)
        {
            throw new ApiException(
                ErrorCodes.BannerInvalidTimeWindow, 400,
                "Banner end must be after its start.",
                "يجب أن تكون نهاية البانر بعد بدايته.");
        }
        if (displayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.BannerInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
    }

    private static AdminBannerDetail ToBannerDetail(Banner banner) =>
        new(banner.Id, banner.Title, banner.TitleArabic, banner.Body, banner.BodyArabic,
            banner.ImageUrl, banner.LinkUrl, banner.StartUtc, banner.EndUtc,
            banner.DisplayOrder, banner.IsActive, banner.CreatedAt, banner.UpdatedAt);
}
