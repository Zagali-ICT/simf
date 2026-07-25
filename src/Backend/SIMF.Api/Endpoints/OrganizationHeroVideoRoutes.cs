using Microsoft.AspNetCore.Http;

namespace SIMF.Api.Endpoints;

/// <summary>D-768 — shared route + absolute-URL helpers for the hero background
/// video, used by the admin upload/delete endpoints (to persist the served URL) and
/// the public stream endpoint (to register its route).</summary>
public static class OrganizationHeroVideoRoutes
{
    /// <summary>The public stream route, RELATIVE to the FastEndpoints
    /// <c>RoutePrefix</c> ("api/v1"). Its <c>.mp4</c> suffix is load-bearing: it is
    /// what makes the composed absolute URL pass the app/website hero accept-gate
    /// (<c>LiveStreamUrlPolicy</c> — an https URL whose path ends <c>.mp4</c>).</summary>
    public const string StreamRoute = "/app/organization/hero-video.mp4";

    // Mirrors Program.cs `config.Endpoints.RoutePrefix = "api/v1"` — prepended only
    // for the request-derived fallback URL (dev / direct calls).
    private const string RoutePrefix = "/api/v1";

    /// <summary>The absolute <c>.mp4</c> URL to persist into
    /// <c>OrganizationProfile.BackgroundVideoUrl</c>: the configured public API base
    /// when set, else derived from the incoming <paramref name="request"/> (a direct
    /// dev call; a reverse-proxied deployment should set the config, since the proxy
    /// does not forward the original Host header).</summary>
    public static string ServedUrl(HttpRequest request, string? publicApiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(publicApiBaseUrl))
        {
            return $"{publicApiBaseUrl.TrimEnd('/')}{StreamRoute}";
        }
        return $"{request.Scheme}://{request.Host}{RoutePrefix}{StreamRoute}";
    }
}
