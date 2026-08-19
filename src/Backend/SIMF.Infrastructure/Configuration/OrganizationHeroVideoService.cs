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

/// <summary>Stores + serves the singleton Organization Profile's hero
/// background video through the centralized <see cref="SIMF.Domain.Files.StoredFile"/> store
/// (<c>FileService.OrganizationHeroVideo</c>: public, plaintext, seekable). Upload
/// streams the bytes to disk (never buffered whole), retires any prior video, and
/// points <c>BackgroundVideoUrl</c> at the served <c>.mp4</c> route so the app +
/// website hero accept-gate passes unchanged. Reuses the session-recording pipeline
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

        // "One active hero video per profile". The store has already done this
        // ahead of its own insert, so on this path the call is a no-op kept for
        // the remove path below; see RetireActiveAsync.
        await RetireActiveAsync(keepId: result.Id, actorUserId, cancellationToken);

        // The pointer, not a URL. CreateStreamedAsync already set it through the
        // store's own pointer sync; this assignment is what makes the profile row
        // consistent in the same unit of work, and what covers the case where the
        // profile row did not exist yet.
        var now = timeProvider.SimfNow();
        var profile = await LoadOrCreateProfileAsync(cancellationToken);
        profile.BackgroundVideoFileId = result.Id;
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
        await RetireActiveAsync(keepId: null, actorUserId, cancellationToken);

        var now = timeProvider.SimfNow();
        var profile = await db.OrganizationProfile
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, cancellationToken);

        // Retiring the files above already cleared the pointer through the store,
        // and only if it still named one of them - so a separately pasted external
        // link the admin set by hand survives untouched. The full-string comparison
        // against a recomputed served URL that used to answer this question is gone
        // with the column it compared: it silently stopped matching whenever the
        // configured base URL changed, and then quietly skipped the clear.
        if (profile is not null)
        {
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

    /// <summary>Retire the active hero-video files: on an upload keep exactly the
    /// one just stored, on a remove keep none. Delegates to the file service so the
    /// on-disk bytes are unlinked rather than only the row soft-deleted.
    ///
    /// <para>It keeps the caller's own id rather than "the newest by timestamp",
    /// which is what it used to do. That ordering fell back to comparing Guids
    /// whenever two rows shared a <c>CreatedAt</c> - routine, since a request
    /// stamps one instant - so the survivor was effectively decided by a coin
    /// flip, and the file the caller had just written could be the one deleted.
    /// Nothing noticed while the profile stored a constant served route: whichever
    /// file survived, the URL read the same. It became visible the moment the
    /// profile started pointing at a specific file, because the pointer named the
    /// deleted one and the hero went blank.</para>
    ///
    /// <para>On the UPLOAD path this is now a no-op, and deliberately kept anyway.
    /// The file store retires the owner previous hero video itself, before it
    /// inserts the replacement, because the filtered unique index on
    /// (Service, OwnerEntityId) refuses a second active row - so by the time this
    /// runs the only active row is the one it was told to keep. What it is still
    /// needed for is the REMOVE path, where there is no keepId and everything
    /// goes.</para></summary>
    private async Task RetireActiveAsync(Guid? keepId, Guid actorUserId, CancellationToken ct)
    {
        var active = await db.StoredFiles.AsNoTracking()
            .Where(f => f.IsActive
                && f.Service == FileService.OrganizationHeroVideo
                && f.OwnerEntityId == OrganizationProfile.SingletonId)
            .OrderBy(f => f.CreatedAt).ThenBy(f => f.Id)
            .Select(f => new { f.Id, f.CreatedAt })
            .ToListAsync(ct);

        // On a remove (no keepId) everything goes. On an upload, drop only what
        // sorts BEFORE the file just stored: never this request's own file, and
        // never one a concurrent request stored after it. Deleting "everything
        // but mine" would have had two overlapping uploads delete each other and
        // leave none, which is the property the old timestamp ordering had and
        // the first correction lost.
        var keep = keepId is { } id ? active.FirstOrDefault(f => f.Id == id) : null;
        foreach (var file in active)
        {
            if (keep is not null
                && (file.CreatedAt, file.Id).CompareTo((keep.CreatedAt, keep.Id)) >= 0)
            {
                continue;
            }
            await fileService.DeleteAsync(file.Id, actorUserId, ct);
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
