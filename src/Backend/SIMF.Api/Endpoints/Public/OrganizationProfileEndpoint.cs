// Tests: SIMF.Api.Tests/OrganizationProfileTests.cs
using System.Globalization;
using FastEndpoints;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Organization;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-495 — public, anonymous read of the Organization / About profile
/// (the edition-generic forum config). The app loads it once on boot, caches it
/// on-device and revalidates with the D-173 <c>Last-Modified</c> / <c>If-Modified-Since</c>
/// → <c>304</c> handshake. Only public branding fields are projected; the in-process
/// cache + the 304 path keep the anonymous surface cheap.</summary>
public sealed class GetOrganizationProfileEndpoint(IOrganizationProfileReadService service)
    : EndpointWithoutRequest<ApiResult<OrganizationProfileResponse>>
{
    public override void Configure()
    {
        Get("/app/organization-profile");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await service.GetAsync(ct);

        // D-173 — truncate to the second (HTTP date precision) before comparing and
        // before emitting Last-Modified, else a sub-second drift makes the next
        // request a needless cache miss. Emit Last-Modified on BOTH branches (incl.
        // the 304) so a cache that refreshes its validator from the 304 keeps it.
        var lastModifiedSecond = snapshot.LastModified.AddTicks(
            -(snapshot.LastModified.Ticks % TimeSpan.TicksPerSecond));
        HttpContext.Response.Headers.LastModified =
            lastModifiedSecond.UtcDateTime.ToString("R");

        // HTTP-date is always RFC 1123 GMT — parse it culture-invariantly and
        // assume UTC so a non-en server culture / timezone can't shift the compare.
        var ifModifiedSince = HttpContext.Request.Headers.IfModifiedSince;
        if (ifModifiedSince.Count > 0
            && DateTimeOffset.TryParse(
                ifModifiedSince.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var since)
            && since >= lastModifiedSecond)
        {
            await Send.ResultAsync(Results.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        await Send.OkAsync(
            ApiResult<OrganizationProfileResponse>.Ok(snapshot.Profile), ct);
    }
}
