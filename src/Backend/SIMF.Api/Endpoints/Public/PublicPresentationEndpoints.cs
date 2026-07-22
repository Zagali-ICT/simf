// Tests: SIMF.Api.Tests/PublicPresentationsTests.cs
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Public;

/// <summary>Wave 2 (Figma 1388:7621 "عروض الجلسات") —
/// <c>GET /api/v1/app/presentations</c>: every active session-presentation file,
/// time-ordered by session start so the app groups by day. Each item carries the
/// session (title + start) + the presenting speaker + the file metadata; the
/// bytes come from the download route. ANONYMOUS (owner 2026-07-22 — the "الجلسات"
/// Sessions list is public, opened from the home "Sessions" tile by a guest); the
/// data is active-forum content (session titles/speakers/decks), consistent with
/// the already-public agenda (D-750) and the public website download route below.</summary>
public sealed class ListPublicPresentationsEndpoint(IPublicSpeakerPresentationService service)
    : EndpointWithoutRequest<ApiResult<PublicPresentations>>
{
    public override void Configure()
    {
        Get("/app/presentations");
        AllowAnonymous();
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
/// soft-deleted. ANONYMOUS (owner 2026-07-22 — the Sessions list is public, and
/// the same file bytes are already served anonymously by the website download
/// route below, so this exposes nothing new).</summary>
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
        AllowAnonymous();
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

/// <summary>Website Session-detail "روابط التحميل" (Figma 5991-85840) —
/// <c>GET /api/v1/app/sessions/{sessionId}/downloads/{presentationId}</c>: streams
/// one of the session's presentation files as an attachment. ANONYMOUS (owner
/// decision 2026-07-15 — these files are public on the marketing website); the
/// session scope is the authorisation, so the presentation must belong to that
/// session (both active) or the route 404s. Distinct from the signed-in
/// <c>/app/presentations/{id}/file</c> attendee route above.</summary>
public sealed class DownloadSessionFileRoute
{
    public Guid SessionId { get; set; }
    public Guid PresentationId { get; set; }
}

public sealed class DownloadSessionFileEndpoint(IPublicSpeakerPresentationService service)
    : Endpoint<DownloadSessionFileRoute>
{
    public override void Configure()
    {
        Get("/app/sessions/{sessionId:guid}/downloads/{presentationId:guid}");
        AllowAnonymous();
        Tags("Public");
        Summary(summary => summary.Summary =
            "Download a public session presentation file (website Session detail).");
    }

    public override async Task HandleAsync(DownloadSessionFileRoute req, CancellationToken ct)
    {
        var file = await service.GetFileAsync(req.SessionId, req.PresentationId, ct);
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
