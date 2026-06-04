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
                (item.Title != null && EF.Functions.Like(item.Title, $"%{term}%"))
                || (item.TitleArabic != null && EF.Functions.Like(item.TitleArabic, $"%{term}%"))
                || (item.Album != null && EF.Functions.Like(item.Album, $"%{term}%"))
                || (item.AlbumArabic != null && EF.Functions.Like(item.AlbumArabic, $"%{term}%")));
        }
        // CP grid per-column filters (D-256). Keys match the SimfDataGrid
        // column Key values on MediaList.razor; unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "title":
                    rows = rows.Where(item => item.Title != null && item.Title.Contains(v));
                    break;
                case "titlearabic":
                    rows = rows.Where(item => item.TitleArabic != null && item.TitleArabic.Contains(v));
                    break;
                case "album":
                    rows = rows.Where(item => item.Album != null && item.Album.Contains(v));
                    break;
                case "albumarabic":
                    rows = rows.Where(item => item.AlbumArabic != null && item.AlbumArabic.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(item => item.IsActive == isActive);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-256). Default preserves DisplayOrder,
        // then newest-first by CreatedAt.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("kind", true) => rows.OrderByDescending(item => item.Kind),
            ("kind", false) => rows.OrderBy(item => item.Kind),
            ("title", true) => rows.OrderByDescending(item => item.Title),
            ("title", false) => rows.OrderBy(item => item.Title),
            ("isactive", true) => rows.OrderByDescending(item => item.IsActive),
            ("isactive", false) => rows.OrderBy(item => item.IsActive),
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
                item.Title,
                item.TitleArabic,
                item.Album,
                item.AlbumArabic,
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
        Validate(request.Kind, request.Title, request.TitleArabic,
            request.Album, request.AlbumArabic, request.Url, request.DisplayOrder);

        var now = timeProvider.GetUtcNow();
        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind,
            Title = NullIfBlank(request.Title),
            TitleArabic = NullIfBlank(request.TitleArabic),
            Album = NullIfBlank(request.Album),
            AlbumArabic = NullIfBlank(request.AlbumArabic),
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

        Validate(request.Kind, request.Title, request.TitleArabic,
            request.Album, request.AlbumArabic, request.Url, request.DisplayOrder);

        item.Kind = request.Kind;
        item.Title = NullIfBlank(request.Title);
        item.TitleArabic = NullIfBlank(request.TitleArabic);
        item.Album = NullIfBlank(request.Album);
        item.AlbumArabic = NullIfBlank(request.AlbumArabic);
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
        MediaKind kind, string? title, string? titleArabic,
        string? album, string? albumArabic, string? url, int displayOrder)
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
        EnsureMaxLength(title, 200, "Title (English)", "العنوان (الإنجليزي)");
        EnsureMaxLength(titleArabic, 200, "Title (Arabic)", "العنوان (العربي)");
        EnsureMaxLength(album, 200, "Album (English)", "الألبوم (الإنجليزي)");
        EnsureMaxLength(albumArabic, 200, "Album (Arabic)", "الألبوم (العربي)");
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
            item.Title,
            item.TitleArabic,
            item.Album,
            item.AlbumArabic,
            item.ImageRelativePath != null,
            item.ThumbnailRelativePath != null,
            item.Url,
            item.DisplayOrder,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt);
}
