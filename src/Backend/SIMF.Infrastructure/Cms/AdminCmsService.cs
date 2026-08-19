// Tests: SIMF.Api.Tests/CmsTests.cs, SIMF.Api.Tests/GridDateSortKeyTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Cms.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Cms;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Cms;

/// <summary>
/// Admin CRUD over content blocks and banners. Both entities live on the App DB;
/// logical FK on <c>LastUpdatedByUserId</c> to <c>SimfUser</c> on the Identity DB.
/// </summary>
internal sealed class AdminCmsService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminCmsService> logger) : IAdminCmsService
{
    /// <summary>Mirrors <c>BannerConfiguration</c>'s stored width for
    /// <c>LinkUrl</c>. Validation has to quote the column's own number: a
    /// tracking or CDN link longer than the column holds otherwise reaches SQL
    /// Server and fails the write, surfacing as a 500 where every other banner
    /// field answers a bilingual 400.</summary>
    private const int BannerLinkUrlMaxLength = 1024;

    /// <summary>
    /// The grid contract for /admin/content-blocks: one entry per key
    /// ContentBlocksList.razor can send, as both its filter and its sort. A key not
    /// declared here is a 400, not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<ContentBlock> ContentBlockColumns =
        new GridColumns<ContentBlock>()
            .Add("key", block => block.Key, searchable: true)
            .Add("content", block => block.Content, searchable: true)
            .Add("contentArabic", block => block.ContentArabic, searchable: true)
            .Add("lastUpdatedAt", block => block.LastUpdatedAt)
            .Add("isActive", block => block.IsActive)
            .DefaultOrder("key")
            .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<ContentBlock, AdminContentBlockSummary>>
        ToContentBlockSummary =
            block => new AdminContentBlockSummary(
                block.Id, block.Key, block.Content, block.ContentArabic, block.IsActive,
                block.LastUpdatedAt, block.LastUpdatedByUserId);

    public Task<GridPage<AdminContentBlockSummary>> ListContentBlocksAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.ContentBlocks.ToGridPageAsync(
            query, ContentBlockColumns, block => block.Id,
            ToContentBlockSummary, cancellationToken);

    public async Task<AdminContentBlockSummary?> GetContentBlockAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalised = NormaliseKey(key);
        var row = await appDbContext.ContentBlocks
            .AsNoTracking()
            .SingleOrDefaultAsync(block => block.Key == normalised, cancellationToken);
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
        var content = request.Content ?? string.Empty;
        var contentArabic = request.ContentArabic ?? string.Empty;

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

        var now = timeProvider.SimfNow();
        var existing = await appDbContext.ContentBlocks
            .SingleOrDefaultAsync(block => block.Key == key, cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.ContentBlockUpserted, actorUserId, $"key={key}", cancellationToken);

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
            .SingleOrDefaultAsync(block => block.Key == normalised, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ContentBlockNotFound, 404,
                "Content block not found.",
                "لم يتم العثور على المحتوى.");

        if (!existing.IsActive)
        {
            return; // idempotent
        }
        existing.IsActive = false;
        existing.LastUpdatedAt = timeProvider.SimfNow();
        existing.LastUpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ContentBlockDeactivated,
            actorUserId,
            $"key={normalised}",
            cancellationToken);
    }

    /// <summary>
    /// The grid contract for /admin/banners: one entry per key BannersList.razor can
    /// send. "start" / "end" are the grid's own column keys — they once read
    /// "startutc" / "endutc", left behind when the persisted columns were renamed,
    /// and the switch's catch-all swallowed the mismatch so neither date column
    /// sorted at all. GridDateSortKeyTests pins them.
    /// </summary>
    private static readonly GridColumns<Banner> BannerColumns = new GridColumns<Banner>()
        .Add("title", banner => banner.Title, searchable: true)
        .Add("titleArabic", banner => banner.TitleArabic, searchable: true)
        .Add("start", banner => banner.Start)
        .Add("end", banner => banner.End)
        .Add("displayOrder", banner => banner.DisplayOrder)
        .Add("isActive", banner => banner.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("start")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<Banner, AdminBannerSummary>> ToBannerSummary =
        banner => new AdminBannerSummary(
            banner.Id, banner.Title, banner.TitleArabic,
            banner.Start, banner.End, banner.DisplayOrder, banner.IsActive, banner.CreatedAt,
            // Round-trip body + link through the grid Excel export. The image is not
            // a column any more: it is uploaded, so there is nothing for a
            // spreadsheet to carry.
            banner.Body, banner.BodyArabic, banner.LinkUrl);

    public Task<GridPage<AdminBannerSummary>> ListBannersAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.Banners.ToGridPageAsync(
            query, BannerColumns, banner => banner.Id, ToBannerSummary, cancellationToken);

    public async Task<AdminBannerDetail?> GetBannerAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.Banners
            .AsNoTracking()
            .SingleOrDefaultAsync(banner => banner.Id == id, cancellationToken);
        return row is null ? null : ToBannerDetail(row);
    }

    public async Task<AdminBannerDetail> CreateBannerAsync(
        Guid actorUserId,
        CreateBannerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBanner(request.Title, request.TitleArabic,
            request.Body, request.BodyArabic, request.LinkUrl, request.Start,
            request.End, request.DisplayOrder);

        var now = timeProvider.SimfNow();
        var banner = new Banner
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            TitleArabic = request.TitleArabic.Trim(),
            Body = request.Body.Trim(),
            BodyArabic = request.BodyArabic.Trim(),
            LinkUrl = NullIfBlank(request.LinkUrl),
            Start = request.Start,
            End = request.End,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Banners.Add(banner);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BannerCreated, actorUserId, $"bannerId={banner.Id}", cancellationToken);

        return ToBannerDetail(banner);
    }

    public async Task<AdminBannerDetail> UpdateBannerAsync(
        Guid actorUserId, Guid id, UpdateBannerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBanner(request.Title, request.TitleArabic,
            request.Body, request.BodyArabic, request.LinkUrl, request.Start,
            request.End, request.DisplayOrder);

        var banner = await appDbContext.Banners
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BannerNotFound, 404,
                "Banner not found.",
                "لم يتم العثور على البانر.");

        banner.Title = request.Title.Trim();
        banner.TitleArabic = request.TitleArabic.Trim();
        banner.Body = request.Body.Trim();
        banner.BodyArabic = request.BodyArabic.Trim();
        banner.LinkUrl = NullIfBlank(request.LinkUrl);
        banner.Start = request.Start;
        banner.End = request.End;
        banner.DisplayOrder = request.DisplayOrder;
        banner.IsActive = request.IsActive;
        banner.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BannerUpdated,
            actorUserId,
            $"bannerId={banner.Id}; active={banner.IsActive}",
            cancellationToken);

        return ToBannerDetail(banner);
    }

    public async Task DeactivateBannerAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var banner = await appDbContext.Banners
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BannerNotFound, 404,
                "Banner not found.",
                "لم يتم العثور على البانر.");

        if (!banner.IsActive)
        {
            return; // idempotent
        }
        banner.IsActive = false;
        banner.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BannerDeactivated, actorUserId, $"bannerId={banner.Id}", cancellationToken);
    }

    // -- helpers --------------------------------------------------------------

    private static string NormaliseKey(string raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateBanner(
        string title, string titleArabic, string body, string bodyArabic,
        string? linkUrl, DateTime start, DateTime end, int displayOrder)
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
        // The link is optional, but it is a stored column like every field above
        // it, so an over-long one is a 400 here rather than a truncation failure
        // inside SaveChanges. The Excel importer maps the same cell into the same
        // request, so it is covered by the one check.
        if (linkUrl is { Length: > BannerLinkUrlMaxLength })
        {
            throw new ApiException(
                ErrorCodes.BannerInvalid, 400,
                $"Banner link URL must be {BannerLinkUrlMaxLength} characters or fewer.",
                $"يجب ألا يتجاوز رابط البانر {BannerLinkUrlMaxLength} حرفاً.");
        }
        if (end <= start)
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
            banner.LinkUrl, banner.Start, banner.End,
            banner.DisplayOrder, banner.IsActive, banner.CreatedAt, banner.UpdatedAt);
}
