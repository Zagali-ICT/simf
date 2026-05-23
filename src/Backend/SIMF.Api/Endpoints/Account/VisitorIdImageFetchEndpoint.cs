// Tests: SIMF.Api.Tests/VisitorProfileTests.cs (round-trip, 404 when missing)
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/account/visitor-profile/id-image</c> — streams the
/// signed-in visitor's ID-image back to the browser (decrypted on the fly
/// from the AES-GCM file). Returns 404 when no image is set. Auth-only —
/// only the owning visitor can read it.
/// </summary>
public sealed class VisitorIdImageFetchEndpoint(IVisitorProfileService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/account/visitor-profile/id-image");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Stream the signed-in visitor's ID image.");
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
