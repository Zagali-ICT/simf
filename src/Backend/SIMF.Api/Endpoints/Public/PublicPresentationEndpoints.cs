// Tests: SIMF.Api.Tests/PublicPresentationsTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Public;

/// <summary>Wave 2 (Figma 1388:7621 "عروض الجلسات") —
/// <c>GET /api/v1/app/presentations</c>: every active session-presentation file,
/// time-ordered by session start so the app groups by day. Each item carries the
/// session (title + start) + the presenting speaker + the file metadata; the
/// bytes come from the download route. Approved account (attendee materials).</summary>
public sealed class ListPublicPresentationsEndpoint(IPublicSpeakerPresentationService service)
    : EndpointWithoutRequest<ApiResult<PublicPresentations>>
{
    public override void Configure()
    {
        Get("/app/presentations");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
        Summary(summary => summary.Summary =
            "Session presentations: downloadable decks grouped by session/day.");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<PublicPresentations>.Ok(await service.ListAsync(ct)), ct);
}

/// <summary>Wave 2 — <c>GET /api/v1/app/presentations/{id}/file</c>: streams one
/// presentation's stored bytes as an attachment (the تحميل button on
/// Figma 1388:7621). A 404 when the presentation / session is missing or
/// soft-deleted. Approved account.</summary>
public sealed class DownloadPublicPresentationRoute
{
    public Guid Id { get; set; }
}

public sealed class DownloadPublicPresentationEndpoint(IPublicSpeakerPresentationService service)
    : Endpoint<DownloadPublicPresentationRoute>
{
    public override void Configure()
    {
        Get("/app/presentations/{id:guid}/file");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Sessions");
        Summary(summary => summary.Summary = "Download a session presentation file.");
    }

    public override async Task HandleAsync(DownloadPublicPresentationRoute req, CancellationToken ct)
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
