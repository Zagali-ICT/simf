// Tests: SIMF.Api.Tests/UserProfileTests.cs (round-trip, 404 when missing)
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/user-profile/id-image</c> — streams the
/// signed-in user's ID-document image back to the browser (decrypted
/// on the fly from the AES-GCM file). Returns 404 when no image is
/// set. Auth-only — only the owning user can read it. Renamed from
/// <c>/account/visitor-profile/id-image</c>.
/// </summary>
public sealed class UserIdDocumentFetchEndpoint(IUserProfileService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/app/account/user-profile/id-image");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Stream the signed-in user's ID-document image.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();

        var image = await service.ReadIdImageAsync(actorId, ct);
        if (image is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // A2-14 (NCA App-Sec Standard) — the ID document (national ID / Iqama /
        // passport) is high-value PII; never let a proxy or the browser cache
        // it to disk. (Avatars keep a short private TTL; ID documents do not.)
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.BytesAsync(image.Content, contentType: image.ContentType, cancellation: ct);
    }
}
