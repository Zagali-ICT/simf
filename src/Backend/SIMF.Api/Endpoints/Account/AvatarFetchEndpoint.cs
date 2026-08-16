// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Account;

/// <summary>Resolves a user's avatar bytes from the unified
/// <c>StoredFile</c> store (App DB, encrypted at rest), owner-scoped by the user id.
/// It is the CALLER-AUTHORIZED read on <c>IFileService</c>, not
/// <c>DownloadAsync</c>: each serve endpoint enforces its own authorization
/// (self-only for the app fetch, an admin View permission for the CP fetch), and the
/// Avatar policy carries a single <c>AdminPermission</c> (Visitors.View) — routing
/// the Others-avatar fetch (gated by Others.View) through DownloadAsync's authz
/// would wrongly deny it. It also preserves the legacy no-per-fetch-audit behaviour
/// for this high-frequency, client-cached read. The integrity re-check and the
/// decrypt still run inside the file service.</summary>
internal static class AvatarBytes
{
    public static async Task<(byte[] Content, string ContentType)?> ReadAsync(
        IFileService files, Guid userId, CancellationToken ct)
    {
        var file = await files.ReadOwnerContentAsync(FileService.Avatar, userId, ct);
        return file is null ? null : (file.Content, file.ContentType ?? "image/png");
    }
}

/// <summary>
/// <c>GET /api/v1/app/account/avatar/{userId:guid}</c> — streams the avatar bytes
/// for the authenticated caller. Authentication is
/// required so the avatar bytes are never enumerable without a token; for the
/// MVP the only caller is the same signed-in user, so this also acts as an
/// authorisation check.
/// </summary>
public sealed class AvatarFetchEndpoint(IFileService files)
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

        var avatar = await AvatarBytes.ReadAsync(files, requestedId, ct);
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
