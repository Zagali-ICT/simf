// Tests: SIMF.Api.Tests/OrganizationHeroVideoTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.Auditing;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Configuration;

/// <summary>D-768 — stores + serves the singleton Organization Profile's hero
/// background video through the centralized <see cref="SIMF.Domain.Files.StoredFile"/> store
/// (<c>FileService.OrganizationHeroVideo</c>: public, plaintext, seekable). Upload
/// streams the bytes to disk (never buffered whole), retires any prior video, and
/// points <c>BackgroundVideoUrl</c> at the served <c>.mp4</c> route so the app +
/// website hero accept-gate passes unchanged. Reuses the D-232 recording pipeline
/// (streamed upload + Range serve); only the access policy differs (public here vs
/// authenticated for a recording).</summary>
internal sealed class OrganizationHeroVideoService(
    SimfAppDbContext db,
    IFileService fileService,
    IOrganizationProfileReadService readCache,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<OrganizationHeroVideoService> logger) : IOrganizationHeroVideoService
{
    public async Task SetAsync(
        Guid actorUserId, Stream content, string fileName, string contentType,
        string extension, string servedUrl, CancellationToken cancellationToken = default)
    {
        // Stream the bytes into the unified store (owner = the profile singleton),
        // plaintext + seekable for Range streaming; the store computes SHA-256 on the
        // fly and never buffers the whole video. The malware scan is skipped (the
        // size-capped, admin-only, extension+MIME-validated policy — same as a
        // recording).
        var result = await fileService.CreateStreamedAsync(
            FileService.OrganizationHeroVideo, OrganizationProfile.SingletonId,
            content, fileName, contentType, extension, actorUserId, cancellationToken);

        // "One active hero video per profile" — retire every other active one now
        // that the replacement is safely stored (unlinks its bytes too), keeping the
        // NEWEST so two concurrent replaces converge on one survivor (never zero).
        await RetireActiveAsync(keepNewest: true, actorUserId, cancellationToken);

        var now = timeProvider.SimfNow();
        var profile = await LoadOrCreateProfileAsync(cancellationToken);
        profile.BackgroundVideoUrl = servedUrl;
        profile.UpdatedAt = now;
        profile.UpdatedBy = actorUserId;
        await db.SaveChangesAsync(cancellationToken);
        readCache.Invalidate();

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganizationProfileUpdated,
            actorUserId,
            $"hero video uploaded; file={result.Id}; bytes={result.SizeBytes}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} uploaded organization hero video {FileId} ({Bytes} bytes)",
            actorUserId, result.Id, result.SizeBytes);
    }

    public async Task RemoveAsync(
        Guid actorUserId, string servedUrl, CancellationToken cancellationToken = default)
    {
        await RetireActiveAsync(keepNewest: false, actorUserId, cancellationToken);

        var now = timeProvider.SimfNow();
        var profile = await db.OrganizationProfile
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, cancellationToken);

        // Clear the URL only when it still points at OUR served route — never wipe a
        // separately-pasted external / YouTube link the admin set by hand.
        if (profile is not null
            && string.Equals(profile.BackgroundVideoUrl, servedUrl, StringComparison.OrdinalIgnoreCase))
        {
            profile.BackgroundVideoUrl = null;
            profile.UpdatedAt = now;
            profile.UpdatedBy = actorUserId;
            await db.SaveChangesAsync(cancellationToken);
            readCache.Invalidate();
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganizationProfileUpdated,
            actorUserId,
            "hero video removed",
            cancellationToken);

        logger.LogInformation("Admin {ActorId} removed the organization hero video", actorUserId);
    }

    public async Task<HeroVideoPointer?> GetActivePointerAsync(
        CancellationToken cancellationToken = default)
    {
        var file = await db.StoredFiles.AsNoTracking()
            .Where(f => f.IsActive
                && f.Service == FileService.OrganizationHeroVideo
                && f.OwnerEntityId == OrganizationProfile.SingletonId
                && f.StorageKey != null)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new { f.Id, f.ContentType, f.OriginalFileName })
            .FirstOrDefaultAsync(cancellationToken);
        if (file is null) { return null; }

        return new HeroVideoPointer(
            file.Id,
            string.IsNullOrEmpty(file.ContentType) ? "video/mp4" : file.ContentType,
            string.IsNullOrEmpty(file.OriginalFileName) ? "hero-video.mp4" : file.OriginalFileName);
    }

    // Retire active hero-video files: on an upload (keepNewest) keep the single
    // NEWEST active and drop the rest; on a remove keep none. Keeping the newest
    // *active* row (not the caller's just-created id) makes two concurrent replaces
    // converge on one survivor instead of each deleting the other's file and leaving
    // zero. Delegates to the file service so the on-disk bytes are unlinked, not just
    // the row soft-deleted.
    private async Task RetireActiveAsync(bool keepNewest, Guid actorUserId, CancellationToken ct)
    {
        var active = await db.StoredFiles.AsNoTracking()
            .Where(f => f.IsActive
                && f.Service == FileService.OrganizationHeroVideo
                && f.OwnerEntityId == OrganizationProfile.SingletonId)
            .OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id)
            .Select(f => f.Id)
            .ToListAsync(ct);
        foreach (var id in keepNewest ? active.Skip(1) : active)
        {
            await fileService.DeleteAsync(id, actorUserId, ct);
        }
    }

    private async Task<OrganizationProfile> LoadOrCreateProfileAsync(CancellationToken ct)
    {
        var profile = await db.OrganizationProfile
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, ct);
        if (profile is null)
        {
            profile = new OrganizationProfile();
            db.OrganizationProfile.Add(profile);
        }
        return profile;
    }
}
