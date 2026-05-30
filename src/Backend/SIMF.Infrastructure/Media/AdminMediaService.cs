// Tests: SIMF.Api.Tests/AdminMediaTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Media.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;
using SIMF.Domain.Media;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Media;

/// <summary>
/// D-199 (Mockup page 30) — admin CRUD over <see cref="MediaItem"/>, built on
/// <see cref="SimfAppDbContext"/>. Modelled on <c>AdminSpeakerService</c>:
/// soft-delete via <c>IsActive</c>, an audit entry per mutation, and the same
/// validation-then-persist flow. There is no unique business key on a media
/// item (unlike Speaker.Code), so there is no 409-duplicate path — see
/// <c>AdminMediaTests</c> and the module notes. Image bytes are written
/// out-of-row through <see cref="IMediaImageStorage"/> (D-90).
/// </summary>
internal sealed class AdminMediaService(
    SimfAppDbContext dbContext,
    IMediaImageStorage imageStorage,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminMediaService> logger) : IAdminMediaService
{
    public async Task<GridPage<AdminMediaSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.MediaItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(item =>
                (item.TitleEn != null && EF.Functions.Like(item.TitleEn, $"%{term}%"))
                || (item.TitleAr != null && EF.Functions.Like(item.TitleAr, $"%{term}%"))
                || (item.AlbumEn != null && EF.Functions.Like(item.AlbumEn, $"%{term}%"))
                || (item.AlbumAr != null && EF.Functions.Like(item.AlbumAr, $"%{term}%")));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(item => item.IsActive == isActive);
        }
        if (query.Filters.TryGetValue("album", out var album)
            && !string.IsNullOrWhiteSpace(album))
        {
            var albumTerm = album.Trim();
            rows = rows.Where(item => item.AlbumEn == albumTerm || item.AlbumAr == albumTerm);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("title", true) => rows.OrderByDescending(item => item.TitleEn),
            ("title", false) => rows.OrderBy(item => item.TitleEn),
            ("displayorder", true) => rows.OrderByDescending(item => item.DisplayOrder),
            _ => rows.OrderBy(item => item.DisplayOrder)
                     .ThenByDescending(item => item.CreatedAt),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(item => new AdminMediaSummary(
                item.Id,
                item.Kind,
                item.TitleEn,
                item.TitleAr,
                item.AlbumEn,
                item.AlbumAr,
                item.ImageRelativePath != null,
                item.Url,
                item.DisplayOrder,
                item.IsActive,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminMediaSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminMediaDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MediaItems
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return item is null ? null : ToDetail(item);
    }

    public async Task<AdminMediaDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.Kind, request.TitleEn, request.TitleAr,
            request.AlbumEn, request.AlbumAr, request.Url, request.DisplayOrder);

        var now = timeProvider.GetUtcNow();
        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind,
            TitleEn = NullIfBlank(request.TitleEn),
            TitleAr = NullIfBlank(request.TitleAr),
            AlbumEn = NullIfBlank(request.AlbumEn),
            AlbumAr = NullIfBlank(request.AlbumAr),
            Url = NullIfBlank(request.Url),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.MediaItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={item.Id}; kind={item.Kind}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created MediaItem {Id} ({Kind})",
            actorUserId, item.Id, item.Kind);

        return ToDetail(item);
    }

    public async Task<AdminMediaDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MediaItems
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.MediaNotFound, 404,
                "The media item was not found.",
                "لم يتم العثور على عنصر الوسائط.");

        Validate(request.Kind, request.TitleEn, request.TitleAr,
            request.AlbumEn, request.AlbumAr, request.Url, request.DisplayOrder);

        item.Kind = request.Kind;
        item.TitleEn = NullIfBlank(request.TitleEn);
        item.TitleAr = NullIfBlank(request.TitleAr);
        item.AlbumEn = NullIfBlank(request.AlbumEn);
        item.AlbumAr = NullIfBlank(request.AlbumAr);
        item.Url = NullIfBlank(request.Url);
        item.DisplayOrder = request.DisplayOrder;
        item.IsActive = request.IsActive;
        item.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={item.Id}; active={item.IsActive}",
        }, cancellationToken);

        return ToDetail(item);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MediaItems
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.MediaNotFound, 404,
                "The media item was not found.",
                "لم يتم العثور على عنصر الوسائط.");

        if (!item.IsActive)
        {
            return; // idempotent
        }

        item.IsActive = false;
        item.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={item.Id}",
        }, cancellationToken);
    }

    public async Task<AdminMediaDetail> SetImageAsync(
        Guid actorUserId,
        Guid id,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MediaItems
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.MediaNotFound, 404,
                "The media item was not found.",
                "لم يتم العثور على عنصر الوسائط.");

        var relativePath = await imageStorage.SaveAsync(
            item.Id, MediaImageSlot.Image, content, contentType, cancellationToken);
        item.ImageRelativePath = relativePath;
        item.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MediaImageSet,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={item.Id}; path={relativePath}",
        }, cancellationToken);

        return ToDetail(item);
    }

    private static void Validate(
        MediaKind kind, string? titleEn, string? titleAr,
        string? albumEn, string? albumAr, string? url, int displayOrder)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ApiException(
                ErrorCodes.MediaInvalid, 400,
                "Media kind is invalid.",
                "نوع الوسائط غير صالح.");
        }
        if (displayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.MediaInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        // Lengths mirror MediaItemConfiguration HasMaxLength values.
        EnsureMaxLength(titleEn, 200, "Title (English)", "العنوان (الإنجليزي)");
        EnsureMaxLength(titleAr, 200, "Title (Arabic)", "العنوان (العربي)");
        EnsureMaxLength(albumEn, 200, "Album (English)", "الألبوم (الإنجليزي)");
        EnsureMaxLength(albumAr, 200, "Album (Arabic)", "الألبوم (العربي)");
        EnsureMaxLength(url, 2048, "URL", "الرابط");

        // A video tile is only useful with a playback URL; an image tile gets
        // its bytes via the separate upload, so Url is optional for images.
        if (kind == MediaKind.Video && string.IsNullOrWhiteSpace(url))
        {
            throw new ApiException(
                ErrorCodes.MediaInvalid, 400,
                "A video media item requires a URL.",
                "يتطلّب عنصر الفيديو رابطاً.");
        }
    }

    private static void EnsureMaxLength(string? value, int max, string fieldEn, string fieldAr)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length > max)
        {
            throw new ApiException(
                ErrorCodes.MediaInvalid, 400,
                $"{fieldEn} must be {max} characters or less.",
                $"يجب ألا يتجاوز {fieldAr} {max} حرفاً.");
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminMediaDetail ToDetail(MediaItem item) =>
        new(item.Id,
            item.Kind,
            item.TitleEn,
            item.TitleAr,
            item.AlbumEn,
            item.AlbumAr,
            item.ImageRelativePath != null,
            item.ThumbnailRelativePath != null,
            item.Url,
            item.DisplayOrder,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt);
}
