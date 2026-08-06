// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SIMF.Api.RequestContext;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Endpoints.Account;

/// <summary>Resolves a user's avatar bytes from the unified
/// <c>StoredFile</c> store (App DB, encrypted at rest), owner-scoped by the user id.
/// It is a RAW decrypt read, not <c>IFileService.DownloadAsync</c>: each serve
/// endpoint enforces its own authorization (self-only for the app fetch, an admin
/// View permission for the CP fetch), and the Avatar policy carries a single
/// <c>AdminPermission</c> (Visitors.View) — routing the Others-avatar fetch (gated
/// by Others.View) through DownloadAsync's authz would wrongly deny it. Skipping
/// DownloadAsync also preserves the legacy no-per-fetch-audit behaviour for this
/// high-frequency, client-cached read.</summary>
internal static class AvatarBytes
{
    public static async Task<(byte[] Content, string ContentType)?> ReadAsync(
        SimfAppDbContext appDb, IFileStorageProvider storage, Guid userId, CancellationToken ct)
    {
        var file = await appDb.StoredFiles.AsNoTracking()
            .Where(f => f.Service == FileService.Avatar && f.OwnerEntityId == userId && f.IsActive)
            // Newest active wins; Id is a deterministic tiebreak for the rare
            // same-tick case (e.g. a fake TimeProvider or a brief replace window).
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Select(f => new { f.StorageKey, f.ContentType, f.IsEncrypted })
            .FirstOrDefaultAsync(ct);
        if (file?.StorageKey is not { Length: > 0 } key) { return null; }
        var bytes = await storage.ReadAsync(key, file.IsEncrypted, ct);
        return bytes is null ? null : (bytes, file.ContentType ?? "image/png");
    }
}

/// <summary>
/// <c>GET /api/v1/app/account/avatar/{userId:guid}</c> — streams the avatar bytes
/// for the authenticated caller (myComment #11, D-039). Authentication is
/// required so the avatar bytes are never enumerable without a token; for the
/// MVP the only caller is the same signed-in user, so this also acts as an
/// authorisation check.
/// </summary>
public sealed class AvatarFetchEndpoint(
    SimfAppDbContext appDb,
    IFileStorageProvider storage)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/app/account/avatar/{userId:guid}");
        Tags("Account");
        Summary(summary => summary.Summary = "Stream the signed-in user's avatar bytes.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var callerId = User.ActorId();

        var requestedId = Route<Guid>("userId");
        // For this increment a caller may only fetch their own avatar. A future
        // module that lets admins view other users' avatars will widen this.
        if (requestedId != callerId)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var avatar = await AvatarBytes.ReadAsync(appDb, storage, requestedId, ct);
        if (avatar is null)
        {
            // No active avatar file (never set, removed, or the bytes are missing)
            // — 404 so the page falls back to the placeholder icon.
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "private, max-age=300";
        await Send.StreamAsync(
            new MemoryStream(avatar.Value.Content), contentType: avatar.Value.ContentType, cancellation: ct);
    }
}
