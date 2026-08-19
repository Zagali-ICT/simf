// Tests: SIMF.Api.Tests/AssetEndpointsTests.cs
using System.Data.SqlTypes;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Assets;
using SIMF.Domain.Files;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Files;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Assets;

/// <summary>The unified media-asset service, now
/// backed by the centralized <see cref="StoredFile"/> store instead of the legacy
/// <c>Asset</c> table and its own storage service. The <see cref="IAssetService"/>
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
    TimeProvider timeProvider,
    ILogger<AssetService> logger) : IAssetService
{
    // The image categories the Media Library manages, each mapped to its
    // StoredFile service. Any StoredFile whose service is outside this set (avatar,
    // ID document, presentation, …) is NOT an "asset" and never surfaces here.
    // The count is deliberately not spelled out: it went stale the first time a
    // category was added, and MappedCategories below is the checkable answer.
    private static readonly IReadOnlyDictionary<AssetCategory, FileService> CategoryToService =
        new Dictionary<AssetCategory, FileService>
        {
            [AssetCategory.SpeakerPhoto] = FileService.SpeakerPhoto,
            [AssetCategory.MediaPartnerLogo] = FileService.MediaPartnerLogo,
            [AssetCategory.SponsorLogo] = FileService.SponsorLogo,
            [AssetCategory.ArchiveCover] = FileService.ArchiveCover,
            [AssetCategory.ArchivePastSpeakerPhoto] = FileService.ArchivePastSpeakerPhoto,
            [AssetCategory.ArchiveGalleryImage] = FileService.ArchiveGalleryImage,
            [AssetCategory.NewsImage] = FileService.NewsImage,
            [AssetCategory.ProgrammeDayImage] = FileService.ProgrammeDayImage,
            [AssetCategory.OrganizationLogo] = FileService.OrganizationLogo,
            [AssetCategory.Banner] = FileService.Banner,
            [AssetCategory.BoothLogo] = FileService.BoothLogo,
            [AssetCategory.ExhibitorLogo] = FileService.ExhibitorLogo,
        };

    private static readonly IReadOnlyDictionary<FileService, AssetCategory> ServiceToCategory =
        CategoryToService.ToDictionary(kv => kv.Value, kv => kv.Key);

    // The scope every /admin/assets read is confined to: only the StoredFile
    // services the Media Library owns, so no filter and no id can reach an avatar
    // or an ID document. Declared after CategoryToService because it reads it.
    private static readonly List<FileService> AssetServices = CategoryToService.Values.ToList();

    /// <summary>
    /// The grid contract for /admin/assets. Every filter here is hand-written,
    /// because the Media Library speaks the ASSET vocabulary (category, kind,
    /// source type) while the rows are <see cref="StoredFile"/>s that store the
    /// file vocabulary (service, file type, source type) — so each key is a
    /// translation, not a comparison. A key not declared here is a 400, and a value
    /// that names no known category / kind / source is a 400 rather than a filter
    /// that quietly does nothing.
    /// Kept beside the two dictionaries it translates through.
    /// </summary>
    private static readonly GridColumns<StoredFile> Columns = new GridColumns<StoredFile>()
        .Add("isActive", file => file.IsActive)
        .Add("createdAt", file => file.CreatedAt)
        .AddFilter("category", raw =>
        {
            if (!Enum.TryParse<AssetCategory>(raw, ignoreCase: true, out var category)
                || !CategoryToService.TryGetValue(category, out var service))
            {
                throw GridFilters.ValueInvalid(
                    "category", raw,
                    $"one of: {string.Join(", ", CategoryToService.Keys)}");
            }
            return file => file.Service == service;
        })
        .AddFilter("kind", raw =>
        {
            if (!Enum.TryParse<AssetKind>(raw, ignoreCase: true, out var kind)
                || !Enum.IsDefined(kind))
            {
                throw GridFilters.ValueInvalid(
                    "kind", raw,
                    $"one of: {string.Join(", ", Enum.GetNames<AssetKind>())}");
            }
            var fileTypes = FileTypesFor(kind);
            return file => fileTypes.Contains(file.FileType);
        })
        .AddFilter("sourceType", raw =>
        {
            if (!Enum.TryParse<AssetSourceType>(raw, ignoreCase: true, out var sourceType)
                || !Enum.IsDefined(sourceType))
            {
                throw GridFilters.ValueInvalid(
                    "sourceType", raw,
                    $"one of: {string.Join(", ", Enum.GetNames<AssetSourceType>())}");
            }
            var source = sourceType == AssetSourceType.ExternalLink
                ? FileSourceType.ExternalLink
                : FileSourceType.Upload;
            return file => file.SourceType == source;
        })
        .DefaultOrder("createdAt", descending: true)
        .PageSize(fallback: 25, max: 200);

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

        // The retire that used to run here has moved into IFileService, which now
        // deactivates the owner previous file BEFORE inserting the replacement.
        // It had to move: the filtered unique index on (Service, OwnerEntityId)
        // rejects a second active row, so retiring afterwards is no longer a legal
        // order. Doing it in the store also means the invariant holds for every
        // caller rather than only for uploads that came through the Media Library.

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
        // Newest first, tie-broken on the id. Every category the Media Library
        // owns is now in FileServicePolicies.SingleActivePerOwner, so the filtered
        // unique index on (Service, OwnerEntityId) guarantees at most one active
        // row and this ordering has nothing left to choose between. It stays as
        // the belt to that braces: it costs nothing on a single-row read, and it
        // is what kept the public serve deterministic for the years before the
        // database held the invariant.
        var file = await dbContext.StoredFiles.AsNoTracking()
            .Where(f => f.Service == service && f.OwnerEntityId == ownerId && f.IsActive)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken);
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

        var content = await fileService.ReadContentAsync(file.Id, cancellationToken);
        if (content is null) { return null; }
        return new AssetResolution(
            AssetSourceType.Upload, ToKind(file.FileType), content.Content, content.ContentType, null);
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
        var now = timeProvider.SimfNow();
        return category switch
        {
            AssetCategory.SpeakerPhoto => dbContext.Speakers
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.MediaPartnerLogo => dbContext.MediaPartners
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.SponsorLogo => dbContext.Sponsors
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            AssetCategory.ArchiveCover => dbContext.ArchiveEditions
                .AnyAsync(x => x.Id == ownerId && x.IsActive, cancellationToken),
            // A past-edition child is public exactly while its edition is: the
            // child carries no active flag of its own, the parent's governs.
            AssetCategory.ArchivePastSpeakerPhoto => dbContext.ArchivePastSpeakers
                .AnyAsync(x => x.Id == ownerId && x.Edition!.IsActive, cancellationToken),
            AssetCategory.ArchiveGalleryImage => dbContext.ArchiveMediaItems
                .AnyAsync(x => x.Id == ownerId && x.Edition!.IsActive, cancellationToken),
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
                        && x.Start <= now && x.End >= now,
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
        // Paged as entities rather than projected: the row's category and owner name
        // are resolved in C# from a polymorphic owner table, which no SELECT carries.
        var page = await dbContext.StoredFiles
            .Where(file => AssetServices.Contains(file.Service))
            .ToGridPageAsync(query, Columns, file => file.Id, file => file, cancellationToken);

        var names = await ResolveOwnerNamesAsync(page.Items, cancellationToken);
        var items = page.Items.Select(file => ToSummary(file, names)).ToList();
        return GridPage<AdminAssetSummary>.Of(items, page.Total, page.Skip, page.Top);
    }

    public async Task<AdminAssetSummary?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && AssetServices.Contains(f.Service), cancellationToken);
        if (file is null) { return null; }
        var names = await ResolveOwnerNamesAsync([file], cancellationToken);
        return ToSummary(file, names);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id && AssetServices.Contains(f.Service), cancellationToken)
            ?? throw NotFound();
        if (!file.IsActive) { return; }
        file.Deactivate();
        file.UpdatedBy = actorUserId;
        file.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await OwnerPointerSync.ClearIfPointingAtAsync(
            dbContext, file.Service, file.OwnerEntityId, file.Id, cancellationToken);
    }

    public async Task RestoreAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.StoredFiles
            .FirstOrDefaultAsync(f => f.Id == id && AssetServices.Contains(f.Service), cancellationToken)
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
        file.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await OwnerPointerSync.PointAtAsync(
            dbContext, file.Service, file.OwnerEntityId, file.Id, cancellationToken);
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
        StoredFile file, IReadOnlyDictionary<(AssetCategory, Guid), string> names)
    {
        var category = ServiceToCategory[file.Service];
        var ownerId = file.OwnerEntityId ?? Guid.Empty;
        return new AdminAssetSummary(
            file.Id, category, ownerId,
            names.GetValueOrDefault((category, ownerId)),
            ToKind(file.FileType),
            file.SourceType == FileSourceType.ExternalLink
                ? AssetSourceType.ExternalLink
                : AssetSourceType.Upload,
            file.ExternalUrl, file.ContentType, file.SizeBytes,
            file.OriginalFileName, file.IsActive, file.CreatedAt, file.UpdatedAt);
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
                case AssetCategory.ArchivePastSpeakerPhoto:
                    foreach (var r in await dbContext.ArchivePastSpeakers.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.NameEn })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.ArchivePastSpeakerPhoto, r.Id)] = r.NameEn;
                    break;
                case AssetCategory.ArchiveGalleryImage:
                    foreach (var r in await dbContext.ArchiveMediaItems.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CaptionEn })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.ArchiveGalleryImage, r.Id)] = r.CaptionEn ?? "Gallery item";
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
                // These two are mapped categories that were never resolved here,
                // so the Media Library rendered them with an empty Owner column
                // while every neighbouring row showed a name - and two programme
                // day images could not be told apart at all.
                case AssetCategory.ProgrammeDayImage:
                    foreach (var r in await dbContext.ProgrammeDays.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Title, x.Date })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.ProgrammeDayImage, r.Id)] =
                            string.IsNullOrEmpty(r.Title) ? r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : r.Title;
                    break;
                case AssetCategory.OrganizationLogo:
                    foreach (var r in await dbContext.OrganizationProfile.AsNoTracking()
                        .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.NameArabic })
                        .ToListAsync(cancellationToken))
                        result[(AssetCategory.OrganizationLogo, r.Id)] =
                            string.IsNullOrEmpty(r.Name) ? r.NameArabic : r.Name;
                    break;
            }
        }
        return result;
    }
}
