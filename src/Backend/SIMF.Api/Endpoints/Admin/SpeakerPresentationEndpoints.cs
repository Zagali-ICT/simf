// Tests: SIMF.Api.Tests/SpeakerPresentationsTests.cs
using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>P2.3 — D-228 (FR-407, SIMF-FDS-004 §5.3): speaker presentation
/// files. Management is gated by the existing Speakers.* permissions (View to
/// read/list/download, Edit to upload/delete). Bytes are stored out-of-row.</summary>
public sealed class ListSpeakerPresentationsRoute { public Guid SpeakerId { get; set; } }

public sealed class ListSpeakerPresentationsEndpoint(IAdminSpeakerPresentationService service)
    : Endpoint<ListSpeakerPresentationsRoute, ApiResult<IReadOnlyList<AdminSpeakerPresentationRow>>>
{
    public override void Configure()
    {
        Get("/admin/speakers/{speakerId:guid}/presentations");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }
    public override async Task HandleAsync(ListSpeakerPresentationsRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<AdminSpeakerPresentationRow>>.Ok(
            await service.ListForSpeakerAsync(req.SpeakerId, ct)), ct);
}

/// <summary>Multipart upload. The file rides the form ("file"); the speaker is
/// the route id and the session is a query param (?sessionId=…), matching the
/// CP <c>simfAccount.uploadFile</c> helper which posts only the file.</summary>
public sealed class UploadSpeakerPresentationRequest
{
    public Guid SpeakerId { get; set; }
    public Guid SessionId { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class UploadSpeakerPresentationEndpoint(IAdminSpeakerPresentationService service)
    : Endpoint<UploadSpeakerPresentationRequest, ApiResult<AdminSpeakerPresentationRow>>
{
    public override void Configure()
    {
        Post("/admin/speakers/{speakerId:guid}/presentations");
        AllowFileUploads();
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UploadSpeakerPresentationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var file = req.File;
        if (file is null || file.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.SpeakerPresentationInvalid, 400,
                "No file was uploaded.",
                "لم يتم رفع أي ملف.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        await Send.OkAsync(ApiResult<AdminSpeakerPresentationRow>.Ok(
            await service.UploadAsync(
                actorId, req.SpeakerId, req.SessionId,
                bytes, file.FileName, contentType, ct)), ct);
    }
}

public sealed class DownloadSpeakerPresentationRoute { public Guid Id { get; set; } }

public sealed class DownloadSpeakerPresentationEndpoint(IAdminSpeakerPresentationService service)
    : Endpoint<DownloadSpeakerPresentationRoute>
{
    public override void Configure()
    {
        Get("/admin/speaker-presentations/{id:guid}/file");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(DownloadSpeakerPresentationRoute req, CancellationToken ct)
    {
        var file = await service.GetFileAsync(req.Id, ct);
        if (file is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{Uri.EscapeDataString(file.Value.FileName)}\"";
        await Send.BytesAsync(
            file.Value.Content, contentType: file.Value.ContentType, cancellation: ct);
    }
}

public sealed class DeleteSpeakerPresentationRoute { public Guid Id { get; set; } }

public sealed class DeleteSpeakerPresentationEndpoint(IAdminSpeakerPresentationService service)
    : Endpoint<DeleteSpeakerPresentationRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/speaker-presentations/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }
    public override async Task HandleAsync(DeleteSpeakerPresentationRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeleteAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
