// Tests: SIMF.Api.Tests/UserProfileTests.cs (round-trip, 404 when missing)
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/account/user-profile/id-image</c> — streams the
/// signed-in user's ID-document image back to the browser (decrypted
/// on the fly from the AES-GCM file). Returns 404 when no image is
/// set. Auth-only — only the owning user can read it. Renamed from
/// <c>/account/visitor-profile/id-image</c> (decisions D-046 b,
/// P8 — D-049).
/// </summary>
public sealed class UserIdDocumentFetchEndpoint(IUserProfileService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/account/user-profile/id-image");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Stream the signed-in user's ID-document image.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var image = await service.ReadIdImageAsync(actorId, ct);
        if (image is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // The cache headers mirror the avatar — short private TTL so a
        // re-upload becomes visible quickly while still saving round trips
        // on routine page loads.
        HttpContext.Response.Headers.CacheControl = "private, max-age=300";
        await Send.BytesAsync(image.Content, contentType: image.ContentType, cancellation: ct);
    }
}
