// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/account/avatar/{userId:guid}</c> — streams the avatar bytes
/// for the authenticated caller (myComment #11, D-039). Authentication is
/// required so the avatar bytes are never enumerable without a token; for the
/// MVP the only caller is the same signed-in user, so this also acts as an
/// authorisation check.
/// </summary>
public sealed class AvatarFetchEndpoint(
    IUserAccountRepository accounts,
    IAvatarStorage avatarStorage)
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
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var callerId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var requestedId = Route<Guid>("userId");
        // For this increment a caller may only fetch their own avatar. A future
        // module that lets admins view other users' avatars will widen this.
        if (requestedId != callerId)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var user = await accounts.FindByIdAsync(requestedId, ct);
        if (user?.AvatarRelativePath is not { Length: > 0 } path)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var read = await avatarStorage.OpenReadAsync(path, ct);
        if (read is null)
        {
            // The row says we have an avatar but the file is missing —
            // treat as "no avatar"; surface as 404 so the page falls back
            // to the placeholder icon.
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "private, max-age=300";
        await Send.StreamAsync(read.Content, contentType: read.ContentType, cancellation: ct);
    }
}
