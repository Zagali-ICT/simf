// Tests: SIMF.Api.Tests/SessionRecordingTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using SIMF.Api.RequestContext;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Infrastructure.Programme;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>Attach (replace) the session recording.
/// Gated by the Sessions.Publish lifecycle permission. The file rides the
/// multipart form ("file"); the bytes are streamed straight to disk. The body
/// + multipart limits are raised for THIS request only (before the body is
/// read) so a large video uploads without weakening the global DoS posture.</summary>
public sealed class UploadSessionRecordingEndpoint(
    IAdminSessionService service,
    IOptions<SessionRecordingStorageOptions> storageOptions)
    : EndpointWithoutRequest<ApiResult<AdminSessionDetail>>
{
    // The recording must be a video. We resolve the content-type from the file
    // EXTENSION (browser-supplied content-types are unreliable) against this
    // allow-list and store the canonical value — so a recording can never be
    // served as text/html and MIME-confused in the browser (the stream also
    // sends X-Content-Type-Options: nosniff). This is the single normalisation
    // point; read paths echo the stored value verbatim.
    private static readonly Dictionary<string, string> AllowedVideoTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".m4v"] = "video/mp4",
            [".webm"] = "video/webm",
            [".ogg"] = "video/ogg",
            [".ogv"] = "video/ogg",
            [".mov"] = "video/quicktime",
        };

    public override void Configure()
    {
        Post("/admin/sessions/{id:guid}/recording");
        AllowFileUploads();
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Publish),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("id");
        var maxBytes = storageOptions.Value.MaxUploadBytes;

        // NB: this reads the form manually rather than the declarative IFormFile
        // binding of UploadSpeakerPresentationEndpoint — because the body/multipart
        // ceilings MUST be raised BEFORE the body is materialised, and the
        // auto-binder would read it first.
        //
        // Raise the per-request body + multipart ceilings before the body is
        // read (the global Kestrel default would 413 a video). Scoped to this
        // request only — every other endpoint keeps its smaller limit.
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
            throw new ApiException(
                ErrorCodes.SessionRecordingInvalid, 400,
                "No file was uploaded.",
                "لم يتم رفع أي ملف.");
        }
        if (file.Length > maxBytes)
        {
            throw new ApiException(
                ErrorCodes.SessionRecordingInvalid, 400,
                $"The recording exceeds the maximum upload size of {maxBytes} bytes.",
                "حجم التسجيل يتجاوز الحد الأقصى المسموح به.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)
            || !AllowedVideoTypes.TryGetValue(ext, out var contentType))
        {
            throw new ApiException(
                ErrorCodes.SessionRecordingInvalid, 400,
                "The recording must be a video file (mp4, m4v, webm, ogg, mov).",
                "يجب أن يكون التسجيل ملف فيديو (mp4، m4v، webm، ogg، mov).");
        }

        await using var stream = file.OpenReadStream();
        var detail = await service.UploadRecordingAsync(
            actorId, id, stream, file.FileName, contentType, file.Length, ct);
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(detail), ct);
    }
}

public sealed class DeleteSessionRecordingEndpoint(IAdminSessionService service)
    : EndpointWithoutRequest<ApiResult<AdminSessionDetail>>
{
    public override void Configure()
    {
        Delete("/admin/sessions/{id:guid}/recording");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Publish),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = User.ActorId();
        var id = Route<Guid>("id");
        await Send.OkAsync(ApiResult<AdminSessionDetail>.Ok(
            await service.DeleteRecordingAsync(actorId, id, ct)), ct);
    }
}
