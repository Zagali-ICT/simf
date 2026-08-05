// Tests: SIMF.Api.Tests/OrganizationHeroVideoTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIMF.Api.RequestContext;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Organization;
using SIMF.Infrastructure.Configuration;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-768 — upload (replace) the home/landing hero background video. Gated by
/// OrganizationProfile.Manage. The file rides the multipart form ("file"); the bytes
/// stream straight to disk and <c>BackgroundVideoUrl</c> is pointed at the served
/// <c>.mp4</c> route so the Flutter home hero (which cannot render a clipped YouTube
/// WebView on Android — D-761) and the website hero play it. The body + multipart
/// ceilings are raised for THIS request only (before the body is read), so the DoS
/// posture on every other endpoint is unchanged.</summary>
public sealed class UploadOrganizationHeroVideoEndpoint(
    IOrganizationHeroVideoService heroVideo,
    IOrganizationProfileAdminService profile,
    IOptions<OrganizationHeroVideoOptions> options,
    IHostEnvironment env)
    : EndpointWithoutRequest<ApiResult<OrganizationProfileResponse>>
{
    // Resolve the canonical content-type from the file EXTENSION (browser-supplied
    // content-types are unreliable) against this allow-list, and store that value —
    // so the video can never be served as text/html and MIME-confused (the stream
    // also sends X-Content-Type-Options: nosniff). mp4 is the web-safe default; webm
    // is allowed for smaller loops.
    private static readonly Dictionary<string, string> AllowedVideoTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".m4v"] = "video/mp4",
            [".webm"] = "video/webm",
        };

    public override void Configure()
    {
        Post("/admin/organization-profile/hero-video");
        AllowFileUploads();
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OrganizationProfile.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();

        // The persisted URL must be one the app/website hero can actually play: an
        // absolute https .mp4 (LiveStreamUrlPolicy). Behind the reverse proxy the
        // request-derived fallback yields the internal (http) host, so in Production a
        // blank OrganizationHeroVideo:PublicApiBaseUrl would silently store an
        // unplayable URL - reject early with an actionable message instead of a green
        // "success". (Dev/Test keep the request fallback so a local upload still works.)
        var servedUrl = OrganizationHeroVideoRoutes.ServedUrl(
            HttpContext.Request, options.Value.PublicApiBaseUrl);
        if (env.IsProduction() && !LiveStreamUrlPolicy.IsAllowed(servedUrl))
        {
            throw new ApiException(ErrorCodes.OrganizationProfileInvalid, 400,
                "Set OrganizationHeroVideo:PublicApiBaseUrl to the public https API base so the uploaded video can be served.",
                "يجب ضبط عنوان واجهة برمجة التطبيقات العام (https) في OrganizationHeroVideo:PublicApiBaseUrl حتى يمكن عرض الفيديو المرفوع.");
        }

        var maxBytes = options.Value.MaxUploadBytes;

        // Raise the per-request body + multipart ceilings BEFORE the body is read (the
        // global Kestrel default would 413 a video). Scoped to this request only.
        var sizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = maxBytes;
        }
        HttpContext.Features.Set<IFormFeature>(
            new FormFeature(HttpContext.Request,
                new FormOptions { MultipartBodyLengthLimit = maxBytes }));

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            throw new ApiException(ErrorCodes.OrganizationProfileInvalid, 400,
                "No file was uploaded.", "لم يتم رفع أي ملف.");
        }
        if (file.Length > maxBytes)
        {
            throw new ApiException(ErrorCodes.OrganizationProfileInvalid, 400,
                $"The video exceeds the maximum upload size of {maxBytes} bytes.",
                "حجم الفيديو يتجاوز الحد الأقصى المسموح به.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedVideoTypes.TryGetValue(ext, out var contentType))
        {
            throw new ApiException(ErrorCodes.OrganizationProfileInvalid, 400,
                "The hero video must be an mp4, m4v or webm file.",
                "يجب أن يكون فيديو الواجهة بصيغة mp4 أو m4v أو webm.");
        }

        await using var stream = file.OpenReadStream();
        await heroVideo.SetAsync(actorId, stream, file.FileName, contentType, ext, servedUrl, ct);
        await Send.OkAsync(ApiResult<OrganizationProfileResponse>.Ok(await profile.GetAsync(ct)), ct);
    }
}

/// <summary>D-768 — remove the uploaded hero background video (reverts the hero to
/// the banner image / bundled media). Gated by OrganizationProfile.Manage. Clears
/// <c>BackgroundVideoUrl</c> only when it still points at our served route, so a
/// separately-pasted external / YouTube link is left intact.</summary>
public sealed class DeleteOrganizationHeroVideoEndpoint(
    IOrganizationHeroVideoService heroVideo,
    IOrganizationProfileAdminService profile,
    IOptions<OrganizationHeroVideoOptions> options)
    : EndpointWithoutRequest<ApiResult<OrganizationProfileResponse>>
{
    public override void Configure()
    {
        Delete("/admin/organization-profile/hero-video");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.OrganizationProfile.Manage),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var servedUrl = OrganizationHeroVideoRoutes.ServedUrl(
            HttpContext.Request, options.Value.PublicApiBaseUrl);
        await heroVideo.RemoveAsync(actorId, servedUrl, ct);
        await Send.OkAsync(ApiResult<OrganizationProfileResponse>.Ok(await profile.GetAsync(ct)), ct);
    }
}
