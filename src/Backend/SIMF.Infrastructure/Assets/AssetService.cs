// Tests: SIMF.Api.Tests/AssetEndpointsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;
using SIMF.Domain.Files;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Assets;

/// <summary>D-357 / D-568 (Wave C S1) — the unified media-asset service, now
/// backed by the centralized <see cref="StoredFile"/> store instead of the legacy
/// <c>Asset</c> table + <c>IImageAssetStorage</c>. The <see cref="IAssetService"/>
/// contract, every endpoint route and the <see cref="AdminAssetSummary"/> shape are
/// unchanged, so the app / Website / CP see no difference — only the physical
/// storage moved. Each <see cref="AssetCategory"/> maps 1:1 to a
/// <see cref="FileService"/> with the same owner family; uploads and links run
/// through <see cref="IFileService"/> (scan → magic-byte detect → canonical MIME →
/// SHA-256 → policy encryption → audit). The "one active asset per (category,
/// owner)" invariant that the Asset filtered-unique index used to give is now
/// enforced in this service (deactivate-the-prior-row on upload; 409-on-restore).</summary>
internal sealed class AssetService(
    SimfAppDbContext dbContext,
    IFileService fileService,
    IFileStorageProvider storage,
    TimeProvider timeProvider,
    ILogger<AssetService> logger) : IAssetService
{
    // The eight image categories the Media Library manages, each mapped to its
    // StoredFile service. Any StoredFile whose service is outside this set (avatar,
    // ID document, presentation, …) is NOT an "asset" and never surfaces here.
    private static readonly IReadOnlyDictionary<AssetCategory, FileService> CategoryToService =
        new Dictionary<AssetCategory, FileService>
        {
            [AssetCategory.SpeakerPhoto] = FileService.SpeakerPhoto,
            [AssetCategory.CompanyLogo] = FileService.CompanyLogo,
            [AssetCategory.MediaPartnerLogo] = FileService.MediaPartnerLogo,
            [AssetCategory.SponsorLogo] = FileService.SponsorLogo,
            [AssetCategory.ArchiveCover] = FileService.ArchiveCover,
            [AssetCategory.NewsImage] = FileService.NewsImage,
            [AssetCategory.ProgrammeDayImage] = FileService.ProgrammeDayImage,
            [AssetCategory.OrganizationLogo] = FileService.OrganizationLogo,
            [AssetCategory.Banner] = FileService.Banner,
            [AssetCategory.BoothLogo] = FileService.BoothLogo,
            [AssetCategory.ExhibitorLogo] = FileService.ExhibitorLogo,
        };

    private static readonly IReadOnlyDictionary<FileService, AssetCategory> ServiceToCategory =
        CategoryToService.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Every <see cref="AssetCategory"/> mapped to a <see cref="FileService"/>
    /// here — for the guard test that fails the build if a category is added without a
    /// service mapping (mirrors <c>AssetPermissionRegistry.MappedCategories</c> and
    /// <c>FileServicePolicies.All</c>). Without it an unmapped category throws
    /// <see cref="ServiceFor"/>'s 400 only at runtime on first upload/resolve.</summary>
    internal static IReadOnlyCollection<AssetCategory> MappedCategories =>
        (IReadOnlyCollection<AssetCategory>)CategoryToService.Keys;

    public async Task SetUploadAsync(
        Guid actorUserId, AssetCategory category, Guid ownerId, AssetKind kind,
        byte[] content, string contentType, string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        if (kind == AssetKind.Video)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Video assets must be an external link, not an uploaded file.",
                "يجب أن تكون مقاطع الفيديو رابطاً خارجياً، وليست ملفاً مرفوعاً.");
        }
        var service = ServiceFor(category);

        // IFileService runs the full ingest pipeline (scan, magic-byte allow-list,
        // canonical MIME, SHA-256, audit). A non-image is rejected there (400).
        var result = await fileService.UploadAsync(
            new UploadFileCommand(
                service, ownerId, content, originalFileName, contentType, actorUserId, FailClosed: false),
            cancellationToken);

        // "One active per (category, owner)" — retire any prior active file for this
        // owner (unlinks its bytes too) now that the replacement is safely stored.
        await RetirePriorActiveAsync(service, ownerId, keepId: result.Id, actorUserId, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} set {Kind} upload asset {Category}/{OwnerId} ({Id})",
            actorUserId, kind, category, ownerId, result.Id);
    }

    public async Task SetExternalLinkAsync(
        Guid actorUserId, AssetCategory category, Guid ownerId, AssetKind kind,
        string url, CancellationToken cancellationToken = default)
    {
        var service = ServiceFor(category);
        // CreateExternalLinkAsync already upserts by (service, owner) — it replaces
        // the owner's active file in place and frees any orphaned uploaded bytes.
        await fileService.CreateExternalLinkAsync(
            new CreateExternalLinkCommand(service, ownerId, url, actorUserId), cancellationToken);
    }

    public async Task<AssetResolution?> ResolveAsync(
        AssetCategory category, Guid ownerId,
        bool requireOwnerActive = true,
        CancellationToken cancellationToken = default)
    {
        var service = ServiceFor(category);
        var file = await dbContext.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.Service == service && f.OwnerEntityId == ownerId && f.IsActive,
                cancellationToken);
        if (file is null) { return null; }

        // A9 (security) — the anonymous public serve refuses a soft-deleted owner's
        // image (the owner drops off every public list but its serve URL is a
        // deterministic (category, ownerId)). The gated admin preview passes
        // requireOwnerActive=false so the Media Library can still show it.
        if (requireOwnerActive
            && !await OwnerIsActiveAsync(category, ownerId, cancellationToken))
        {
            return null;
        }

        if (file.SourceType == FileSourceType.ExternalLink)
        {
            return new AssetResolution(
                AssetSourceType.ExternalLink, ToKind(file.FileType), null, null, file.ExternalUrl);
        }

        if (string.IsNullOrEmpty(file.StorageKey)) { return null; }
        var bytes = await storage.ReadAsync(file.StorageKey, file.IsEncrypted, cancellationToken);
        if (bytes is null) { return null; }
        return new AssetResolution(
            AssetSourceType.Upload, ToKind(file.FileType), bytes, file.ContentType, null);
    }

    public async Task<IReadOnlySet<Guid>> WhichOwnersHaveActiveAssetAsync(
        AssetCategory category, IReadOnlyCollection<Guid> ownerIds,
        CancellationToken cancellationToken = default)
    {
        if (ownerIds.Count == 0) { return new HashSet<Guid>(); }
        var service = ServiceFor(category);
        var ids = ownerIds.ToList();
        return (await dbContext.StoredFiles.AsNoTracking()
            .Where(file => file.Service == service && file.IsActive
                && file.OwnerEntityId != null && ids.Contains(file.OwnerEntityId.Value))
            .Select(file => file.OwnerEntityId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    // A9 (security) — mirrors the FULL public-visibility predicate of each
    // category's owner table (polymorphic OwnerId, no FK — one AnyAsync per
    // category). NewsImage additionally gates on PublishedAt <= now (an embargoed
    // article is feed-hidden, so its hero image must 404 too). Unmapped = fail-closed.
    private Task<bool> OwnerIsActiveAsync(
        AssetCategory category, Guid ownerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return category switch
        {
            AssetCategory.SpeakerPhoto => dbContext.Speakers
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.CompanyLogo => dbContext.Contacts
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.MediaPartnerLogo => dbContext.MediaPartners
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.SponsorLogo => dbContext.Sponsors
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.ArchiveCover => dbContext.ArchiveEditions
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.NewsImage => dbContext.News
                .AnyAsync(x => x.Id == ownerId && x.IsActive && x.PublishedAt <= now, cancellationToken),
            AssetCategory.ProgrammeDayImage => dbContext.ProgrammeDays
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.OrganizationLogo => dbContext.OrganizationProfile
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            // #43 — a banner image is public only while the banner is active AND
            // within its display window, matching GET /app/banners visibility.
            AssetCategory.Banner => dbContext.Banners
                .AnyAsync(
                    x => x.Id == ownerId && x.IsActive
                        && x.StartUtc <= now && x.EndUtc >= now,
                    cancellationToken),
            // #16 — a booth is publicly hidden when its linked exhibitor is
            // soft-deleted (PublicBoothService mirrors this), so its logo must 404 too.
            AssetCategory.BoothLogo => dbContext.Booths
                .AnyAsync(
                    x => x.Id == ownerId && x.IsActive
                        && (x.Exhibitor == null || x.Exhibitor.IsActive),
                    cancellationToken),
            AssetCategory.ExhibitorLogo => dbContext.Exhibitors
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            _ => Task.FromResult(false),
        };
    }

    public async Task<GridPage<AdminAssetSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var assetServices = CategoryToService.Values.ToList();
        var rows = dbContext.StoredFiles.AsNoTracking()
            .Where(f => assetServices.Contains(f.Service));

        if (query.Filters.TryGetValue("category", out var cat)
            && Enum.TryParse<AssetCategory>(cat, true, out var c) && Enum.IsDefined(c)
            && CategoryToService.TryGetValue(c, out var svc))
        {
            rows = rows.Where(f => f.Service == svc);
        }
        if (query.Filters.TryGetValue("kind", out var k)
            && Enum.TryParse<AssetKind>(k, true, out var kk) && Enum.IsDefined(kk))
        {
            var fileTypes = FileTypesFor(kk);
            rows = rows.Where(f => fileTypes.Contains(f.FileType));
        }
        if (query.Filters.TryGetValue("sourceType", out var st)
            && Enum.TryParse<AssetSourceType>(st, true, out var s) && Enum.IsDefined(s))
        {
            var source = s == AssetSourceType.ExternalLink
                ? FileSourceType.ExternalLink : FileSourceType.Upload;
            rows = rows.Where(f => f.SourceType == source);
        }
        if (query.Filters.TryGetValue("isActive", out var ia) && bool.TryParse(ia, out var active))
        {
            rows = rows.Where(f => f.IsActive == active);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip).Take(top)
            .ToListAsync(cancellationToken);

        var names = await ResolveOwnerNamesAsync(page, cancellationToken);
        var items = page.Select(f => ToSummary(f, names)).ToList();
        return GridPage<AdminAssetSummary>.Of(items, total, skip, top);
    }

    public async Task<AdminAssetSummary?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var assetServices = CategoryToService.Values.ToList();
        var file = await dbContext.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && assetServices.Contains(f.Service), cancellationToken);
        if (file is null) { return null; }
        var names = await ResolveOwnerNamesAsync([file], cancellationToken);
        return ToSummary(file, names);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var assetServices = CategoryToService.Values.ToList();
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id && assetServices.Contains(f.Service), cancellationToken)
            ?? throw NotFound();
        if (!file.IsActive) { return; }
        file.Deactivate();
        file.UpdatedBy = actorUserId;
        file.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var assetServices = CategoryToService.Values.ToList();
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id && assetServices.Contains(f.Service), cancellationToken)
            ?? throw NotFound();
        if (file.IsActive) { return; }
        var conflict = await dbContext.StoredFiles.AnyAsync(
            f => f.Id != id && f.Service == file.Service
                && f.OwnerEntityId == file.OwnerEntityId && f.IsActive,
            cancellationToken);
        if (conflict)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 409,
                "Another active asset already exists for this owner; remove it first.",
                "يوجد أصل نشط آخر لهذا العنصر؛ يرجى إزالته أولاً.");
        }
        file.IsActive = true;
        file.DeletedAt = null;
        file.UpdatedBy = actorUserId;
        file.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Retire every OTHER active file of this (service, owner) so exactly one stays
    // active after an upload (the Asset table's filtered-unique invariant, now
    // service-enforced). Delegates to the file service so the bytes are unlinked.
    private async Task RetirePriorActiveAsync(
        FileService service, Guid ownerId, Guid keepId, Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var priorIds = await dbContext.StoredFiles.AsNoTracking()
            .Where(f => f.Service == service && f.OwnerEntityId == ownerId
                && f.IsActive && f.Id != keepId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);
        foreach (var priorId in priorIds)
        {
            await fileService.DeleteAsync(priorId, actorUserId, cancellationToken);
        }
    }

    private static ApiException NotFound() =>
        new(ErrorCodes.ValidationFailed, 404, "The asset was not found.", "لم يتم العثور على الأصل.");

    private static FileService ServiceFor(AssetCategory category) =>
        CategoryToService.TryGetValue(category, out var service)
            ? service
            : throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Unknown asset category.", "فئة الوسائط غير معروفة.");

    private static AssetKind ToKind(FileType type) => type switch
    {
        FileType.Video => AssetKind.Video,
        FileType.Pdf or FileType.Document or FileType.Spreadsheet => AssetKind.Document,
        _ => AssetKind.Image,
    };

    private static IReadOnlyList<FileType> FileTypesFor(AssetKind kind) => kind switch
    {
        AssetKind.Video => [FileType.Video],
        AssetKind.Document => [FileType.Pdf, FileType.Document, FileType.Spreadsheet],
        _ => [FileType.Image],
    };

    private AdminAssetSummary ToSummary(
        StoredFile f, IReadOnlyDictionary<(AssetCategory, Guid), string> names)
    {
        var category = ServiceToCategory[f.Service];
        var ownerId = f.OwnerEntityId ?? Guid.Empty;
        return new AdminAssetSummary(
            f.Id, category, ownerId,
            names.GetValueOrDefault((category, ownerId)),
            ToKind(f.FileType),
            f.SourceType == FileSourceType.ExternalLink ? AssetSourceType.ExternalLink : AssetSourceType.Upload,
            f.ExternalUrl, f.ContentType, f.SizeBytes,
            f.OriginalFileName, f.IsActive, f.CreatedAt, f.UpdatedAt);
    }

    // Resolve each asset's owner display name from its category's own table
    // (polymorphic OwnerId; one batched query per category present on the page).
    private async Task<Dictionary<(AssetCategory, Guid), string>> ResolveOwnerNamesAsync(
        IReadOnlyList<StoredFile> files, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(AssetCategory, Guid), string>();
        foreach (var group in files.GroupBy(f => ServiceToCategory[f.Service]))
        {
            var ids = group.Select(f => f.OwnerEntityId ?? Guid.Empty).Distinct().ToList();
            switch (group.Key)
            {
                case AssetCategory.SpeakerPhoto:
                    foreach (var r in await dbContext.Speakers.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.SpeakerPhoto, r.Id)] = r.Name;
                    break;
                case AssetCategory.CompanyLogo:
                    foreach (var r in await dbContext.Contacts.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.NameArabic })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.CompanyLogo, r.Id)] = r.Name ?? r.NameArabic;
                    break;
                case AssetCategory.MediaPartnerLogo:
                    foreach (var r in await dbContext.MediaPartners.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.MediaPartnerLogo, r.Id)] = r.Name;
                    break;
                case AssetCategory.SponsorLogo:
                    foreach (var r in await dbContext.Sponsors.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.SponsorLogo, r.Id)] = r.Name;
                    break;
                case AssetCategory.ArchiveCover:
                    foreach (var r in await dbContext.ArchiveEditions.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Year })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.ArchiveCover, r.Id)] = $"SIMF {r.Year}";
                    break;
                case AssetCategory.NewsImage:
                    foreach (var r in await dbContext.News.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Title })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.NewsImage, r.Id)] = r.Title;
                    break;
                case AssetCategory.Banner:
                    foreach (var r in await dbContext.Banners.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Title })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.Banner, r.Id)] = r.Title;
                    break;
                case AssetCategory.BoothLogo:
                    foreach (var r in await dbContext.Booths.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.NameArabic })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.BoothLogo, r.Id)] =
                            string.IsNullOrEmpty(r.Name) ? r.NameArabic : r.Name;
                    break;
                case AssetCategory.ExhibitorLogo:
                    foreach (var r in await dbContext.Exhibitors.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.NameArabic })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.ExhibitorLogo, r.Id)] =
                            string.IsNullOrEmpty(r.Name) ? r.NameArabic : r.Name;
                    break;
            }
        }
        return result;
    }
}
