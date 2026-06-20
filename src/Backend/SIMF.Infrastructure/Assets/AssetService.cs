using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;
using SIMF.Domain.Assets;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Assets;

/// <summary>D-357 — the single unified media-asset service every image-bearing
/// entity shares. Centralises validation (size / content-type / kind), out-of-row
/// storage, the (Category, Owner) upsert, soft-delete and audit in one place;
/// built on <see cref="SimfAppDbContext"/>. <c>CreatedBy</c> / <c>UpdatedBy</c>
/// are stamped by the audit SaveChanges interceptor (same as
/// <c>AdminSpeakerService</c>).</summary>
internal sealed class AssetService(
    SimfAppDbContext dbContext,
    IImageAssetStorage storage,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AssetService> logger) : IAssetService
{
    private const long MaxImageBytes = 5L * 1024 * 1024;
    private const long MaxDocumentBytes = 10L * 1024 * 1024;

    private static readonly HashSet<string> ImageContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp" };

    public async Task SetUploadAsync(
        Guid actorUserId, AssetCategory category, Guid ownerId, AssetKind kind,
        byte[] content, string contentType, string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        ValidateUpload(kind, content, contentType);

        var asset = await GetActiveAsync(category, ownerId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var assetId = asset?.Id ?? Guid.NewGuid();
        var previousUploadPath =
            asset is { SourceType: AssetSourceType.Upload, StoragePath: { Length: > 0 } p } ? p : null;

        var storedPath = await storage.SaveAsync(assetId, content, contentType, cancellationToken);

        if (asset is null)
        {
            asset = new Asset
            {
                Id = assetId,
                Category = category,
                OwnerId = ownerId,
                CreatedAt = now,
                IsActive = true,
            };
            dbContext.Assets.Add(asset);
        }
        else
        {
            asset.UpdatedAt = now;
        }

        asset.Kind = kind;
        asset.SourceType = AssetSourceType.Upload;
        asset.StoragePath = storedPath;
        asset.ExternalUrl = null;
        asset.ContentType = contentType;
        asset.SizeBytes = content.LongLength;
        asset.OriginalFileName = originalFileName;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Remove an orphaned prior file (e.g. extension changed) only after the
        // row is safely persisted, so a failed save never loses the live image.
        if (previousUploadPath is not null && previousUploadPath != storedPath)
        {
            await storage.DeleteAsync(previousUploadPath, cancellationToken);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AssetUploaded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={asset.Id}; category={category}; owner={ownerId}; kind={kind}; bytes={content.LongLength}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} set {Kind} upload asset {Category}/{OwnerId} ({Id})",
            actorUserId, kind, category, ownerId, asset.Id);
    }

    public async Task SetExternalLinkAsync(
        Guid actorUserId, AssetCategory category, Guid ownerId, AssetKind kind,
        string url, CancellationToken cancellationToken = default)
    {
        var normalised = ValidateLink(url);

        var asset = await GetActiveAsync(category, ownerId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var previousUploadPath =
            asset is { SourceType: AssetSourceType.Upload, StoragePath: { Length: > 0 } p } ? p : null;

        if (asset is null)
        {
            asset = new Asset
            {
                Id = Guid.NewGuid(),
                Category = category,
                OwnerId = ownerId,
                CreatedAt = now,
                IsActive = true,
            };
            dbContext.Assets.Add(asset);
        }
        else
        {
            asset.UpdatedAt = now;
        }

        asset.Kind = kind;
        asset.SourceType = AssetSourceType.ExternalLink;
        asset.ExternalUrl = normalised;
        asset.StoragePath = null;
        asset.ContentType = null;
        asset.SizeBytes = null;
        asset.OriginalFileName = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Swapping a previously-uploaded file for a link frees its bytes.
        if (previousUploadPath is not null)
        {
            await storage.DeleteAsync(previousUploadPath, cancellationToken);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AssetLinked,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={asset.Id}; category={category}; owner={ownerId}; kind={kind}; url={normalised}",
        }, cancellationToken);
    }

    public async Task<AssetResolution?> ResolveAsync(
        AssetCategory category, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Category == category && a.OwnerId == ownerId && a.IsActive,
                cancellationToken);
        if (asset is null) { return null; }

        if (asset.SourceType == AssetSourceType.ExternalLink)
        {
            return new AssetResolution(
                AssetSourceType.ExternalLink, asset.Kind, null, null, asset.ExternalUrl);
        }

        if (asset.StoragePath is not { Length: > 0 } path) { return null; }
        var read = await storage.GetAsync(path, cancellationToken);
        if (read is null) { return null; }
        return new AssetResolution(
            AssetSourceType.Upload, asset.Kind, read.Value.Content, read.Value.ContentType, null);
    }

    public async Task<GridPage<AdminAssetSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.Assets.AsNoTracking().AsQueryable();
        if (query.Filters.TryGetValue("category", out var cat)
            && Enum.TryParse<AssetCategory>(cat, true, out var c) && Enum.IsDefined(c))
        {
            rows = rows.Where(a => a.Category == c);
        }
        if (query.Filters.TryGetValue("kind", out var k)
            && Enum.TryParse<AssetKind>(k, true, out var kk) && Enum.IsDefined(kk))
        {
            rows = rows.Where(a => a.Kind == kk);
        }
        if (query.Filters.TryGetValue("sourceType", out var st)
            && Enum.TryParse<AssetSourceType>(st, true, out var s) && Enum.IsDefined(s))
        {
            rows = rows.Where(a => a.SourceType == s);
        }
        if (query.Filters.TryGetValue("isActive", out var ia) && bool.TryParse(ia, out var active))
        {
            rows = rows.Where(a => a.IsActive == active);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip).Take(top)
            .ToListAsync(cancellationToken);

        var names = await ResolveOwnerNamesAsync(page, cancellationToken);
        var items = page.Select(a => ToSummary(a, names)).ToList();
        return GridPage<AdminAssetSummary>.Of(items, total, new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminAssetSummary?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null) { return null; }
        var names = await ResolveOwnerNamesAsync([asset], cancellationToken);
        return ToSummary(asset, names);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw NotFound();
        if (!asset.IsActive) { return; }
        asset.IsActive = false;
        asset.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AssetRemoved,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; category={asset.Category}; owner={asset.OwnerId}",
        }, cancellationToken);
    }

    public async Task RestoreAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw NotFound();
        if (asset.IsActive) { return; }
        var conflict = await dbContext.Assets.AnyAsync(
            a => a.Id != id && a.Category == asset.Category && a.OwnerId == asset.OwnerId && a.IsActive,
            cancellationToken);
        if (conflict)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 409,
                "Another active asset already exists for this owner; remove it first.",
                "يوجد أصل نشط آخر لهذا العنصر؛ يرجى إزالته أولاً.");
        }
        asset.IsActive = true;
        asset.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AssetRestored,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; category={asset.Category}; owner={asset.OwnerId}",
        }, cancellationToken);
    }

    private static ApiException NotFound() =>
        new(ErrorCodes.ValidationFailed, 404, "The asset was not found.", "لم يتم العثور على الأصل.");

    private static AdminAssetSummary ToSummary(
        Asset a, IReadOnlyDictionary<(AssetCategory, Guid), string> names) =>
        new(a.Id, a.Category, a.OwnerId,
            names.GetValueOrDefault((a.Category, a.OwnerId)),
            a.Kind, a.SourceType, a.ExternalUrl, a.ContentType, a.SizeBytes,
            a.OriginalFileName, a.IsActive, a.CreatedAt, a.UpdatedAt);

    // Resolve each asset's owner display name from its category's own table
    // (polymorphic OwnerId; one batched query per category present on the page).
    private async Task<Dictionary<(AssetCategory, Guid), string>> ResolveOwnerNamesAsync(
        IReadOnlyList<Asset> assets, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(AssetCategory, Guid), string>();
        foreach (var group in assets.GroupBy(a => a.Category))
        {
            var ids = group.Select(a => a.OwnerId).Distinct().ToList();
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
            }
        }
        return result;
    }

    private Task<Asset?> GetActiveAsync(
        AssetCategory category, Guid ownerId, CancellationToken cancellationToken) =>
        dbContext.Assets.FirstOrDefaultAsync(
            a => a.Category == category && a.OwnerId == ownerId && a.IsActive, cancellationToken);

    private static void ValidateUpload(AssetKind kind, byte[] content, string contentType)
    {
        if (content is null || content.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "No file was uploaded.", "لم يتم رفع أي ملف.");
        }
        if (kind == AssetKind.Video)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Video assets must be an external link, not an uploaded file.",
                "يجب أن تكون مقاطع الفيديو رابطاً خارجياً، وليست ملفاً مرفوعاً.");
        }
        if (kind == AssetKind.Document)
        {
            if (!string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApiException(ErrorCodes.ValidationFailed, 400,
                    "A document asset must be a PDF.", "يجب أن يكون المستند بصيغة PDF.");
            }
            if (content.LongLength > MaxDocumentBytes)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, 400,
                    "Document must be 10 MB or smaller.", "يجب أن يكون المستند 10 ميجابايت أو أقل.");
            }
            return;
        }

        // Image
        if (!ImageContentTypes.Contains(contentType))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Image must be PNG, JPEG or WebP.", "يجب أن تكون الصورة بصيغة PNG أو JPEG أو WebP.");
        }
        // M1 (security) — the bytes must also carry the matching magic-byte
        // signature, not just the client-declared content type, so a polyglot or
        // renamed file can't be served as an image by the anonymous asset
        // endpoint (parity with the avatar / ID-document uploads).
        if (!ImageUploadValidation.MagicBytesMatch(content, contentType))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Image content does not match its declared type.",
                "محتوى الصورة لا يطابق النوع المحدّد.");
        }
        if (content.LongLength > MaxImageBytes)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Image must be 5 MB or smaller.", "يجب أن تكون الصورة 5 ميجابايت أو أقل.");
        }
    }

    private static string ValidateLink(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        // L3 (security) — external links are served to anonymous visitors via a
        // 302, so the target must be a real, public https host. Require https
        // (no cleartext) and reject literal IPs / localhost / internal TLDs, so
        // the trusted SIMF domain can't be turned into an open redirect to an
        // internal service or an attacker-chosen plain-http endpoint.
        if (trimmed.Length is 0 or > 1024
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !IsPublicHost(uri))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "Provide a valid public https URL (max 1024 characters).",
                "يرجى إدخال رابط https عام صحيح لا يتجاوز 1024 حرفاً.");
        }
        return trimmed;
    }

    /// <summary>L3 (security) — reject non-public link hosts: localhost, the
    /// *.localhost / *.local / *.internal TLDs, and any literal IP (which would
    /// also cover the private / loopback / link-local / cloud-metadata ranges).
    /// A legitimate external media link is always a named public host.</summary>
    private static bool IsPublicHost(Uri uri)
    {
        var host = uri.Host;
        if (string.IsNullOrEmpty(host)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !System.Net.IPAddress.TryParse(host, out _);
    }
}
