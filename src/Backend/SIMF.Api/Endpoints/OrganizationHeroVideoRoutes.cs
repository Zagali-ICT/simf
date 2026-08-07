using Microsoft.AspNetCore.Http;
using SIMF.Common;

namespace SIMF.Api.Endpoints;

/// <summary>Absolute-URL helper for the hero background video, used by the
/// admin upload/delete endpoints to persist the served URL.
///
/// <para>The route itself lives on
/// <see cref="OrganizationHeroVideoRoute.StreamRoute"/> in <c>SIMF.Common</c>,
/// because the Control Panel needs it too and cannot reference this assembly. It
/// was hand-copied there; now there is one constant. Only the composition, which
/// needs <see cref="HttpRequest"/>, stays here.</para></summary>
public static class OrganizationHeroVideoRoutes
{
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
            return $"{publicApiBaseUrl.TrimEnd('/')}{OrganizationHeroVideoRoute.StreamRoute}";
        }
        return $"{request.Scheme}://{request.Host}{RoutePrefix}{OrganizationHeroVideoRoute.StreamRoute}";
    }
}
