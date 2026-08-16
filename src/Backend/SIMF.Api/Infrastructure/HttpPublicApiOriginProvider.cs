using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Infrastructure.Configuration;

namespace SIMF.Api.Infrastructure;

/// <summary>Answers <see cref="IPublicApiOriginProvider"/> from the configured
/// public base URL, falling back to the incoming request.
///
/// <para>That precedence is not a preference, it is the existing behaviour: the
/// hero-video upload endpoint composed the served URL exactly this way before the
/// pointer conversion moved the composition to read time. A reverse-proxied
/// deployment must configure <c>PublicApiBaseUrl</c>, because the proxy does not
/// forward the original Host header; a direct call in development has no such
/// problem and the request answers correctly.</para></summary>
internal sealed class HttpPublicApiOriginProvider(
    IHttpContextAccessor httpContextAccessor,
    IOptions<OrganizationHeroVideoOptions> options) : IPublicApiOriginProvider
{
    public string? GetApiBaseUrl()
    {
        var configured = options.Value.PublicApiBaseUrl?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            return configured.TrimEnd('/');
        }

        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? null : $"{request.Scheme}://{request.Host}/api/v1";
    }
}
