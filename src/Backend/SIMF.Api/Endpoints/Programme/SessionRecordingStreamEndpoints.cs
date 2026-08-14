// Tests: SIMF.Api.Tests/SessionRecordingTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Authentication;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Api.Endpoints.Programme;

/// <summary>Mint a short-lived token to stream one
/// published session's recording. Requires a signed-in approved account (the
/// app user) — recordings are not anonymously enumerable. 404 when the session
/// is not published or has no recording. The token is scoped to this one
/// recording and validated by the dedicated StreamToken scheme.</summary>
public sealed class RequestRecordingStreamTokenRequest { public Guid Id { get; set; } }

public sealed class RequestRecordingStreamTokenEndpoint(
    IProgrammeSessionService sessions, IJwtTokenService tokens)
    : Endpoint<RequestRecordingStreamTokenRequest, ApiResult<RecordingStreamTokenResponse>>
{
    public override void Configure()
    {
        Post("/app/programme/sessions/{id:guid}/recording/token");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Programme");
    }

    public override async Task HandleAsync(
        RequestRecordingStreamTokenRequest req, CancellationToken ct)
    {
        var recording = await sessions.GetPublishedRecordingAsync(req.Id, ct);
        if (recording is null)
        {
            throw new ApiException(
                ErrorCodes.SessionRecordingNotFound, 404,
                "No published recording is available for this session.",
                "لا يوجد تسجيل منشور متاح لهذه الجلسة.");
        }

        var userId = User.ActorId();

        var token = tokens.CreateRecordingStreamToken(req.Id, userId);
        var streamUrl = $"/api/v1/app/programme/sessions/{req.Id}/recording/stream";
        // L1 (security) — the response body carries a bearer stream token; never
        // let a proxy or the browser cache it.
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(ApiResult<RecordingStreamTokenResponse>.Ok(
            new RecordingStreamTokenResponse(
                token.Value, token.ExpiresInSeconds, streamUrl)), ct);
    }
}

/// <summary>Range-stream the recording's MP4 bytes.
/// Authenticated by the StreamToken scheme (token on <c>?access_token=</c>,
/// since an HTML5 <c>&lt;video&gt;</c> cannot set an Authorization header). The
/// token is scoped to one recording, so a mismatched <c>recording_session_id</c>
/// is 403. The publish/recording gate is re-checked here in case the session
/// was un-published since the token was minted. Streams with
/// <c>enableRangeProcessing</c> so the player can seek (HTTP 206).</summary>
public sealed class StreamSessionRecordingRequest { public Guid Id { get; set; } }

public sealed class StreamSessionRecordingEndpoint(
    IProgrammeSessionService sessions, IFileService files)
    : Endpoint<StreamSessionRecordingRequest>
{
    public override void Configure()
    {
        Get("/app/programme/sessions/{id:guid}/recording/stream");
        AuthSchemes(JwtBearerSetup.StreamScheme);
        Tags("Programme");
    }

    public override async Task HandleAsync(
        StreamSessionRecordingRequest req, CancellationToken ct)
    {
        var scoped = User.FindFirstValue("recording_session_id");
        if (!Guid.TryParse(scoped, out var scopedId) || scopedId != req.Id)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        // Deliberate: the StreamToken scheme skips the (Identity-DB) security-stamp
        // revocation check on the hot path, but we DO run this one cheap App-DB
        // PK lookup per range request so an un-publish / recording-delete takes
        // effect immediately (content retraction) rather than lingering for the
        // token's lifetime. A clustered-index seek per range request is negligible.
        var recording = await sessions.GetPublishedRecordingAsync(req.Id, ct);
        if (recording is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // RecordingFileId is a real key into StoredFiles; open it
        // as a seekable plaintext stream (SessionRecording is EncryptAtRest:false) so
        // the player can Range-seek (HTTP 206) without buffering the whole video.
        var file = await files.OpenReadStreamAsync(recording.StoredFileId, ct);
        if (file is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Defence in depth: the recording is opened by ?access_token URL in a
        // browser, so stop MIME-sniffing (the stored content-type is already
        // validated to video/* on upload, but nosniff blocks a polyglot file
        // from being interpreted as HTML in the api origin).
        HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Send.StreamAsync disposes the stream after sending (see
        // AvatarFetchEndpoint). enableRangeProcessing lets the player seek
        // and resume — a large MP4 is never buffered whole in memory.
        await Send.StreamAsync(
            file.Content,
            fileName: recording.FileName,
            fileLengthBytes: file.Length,
            contentType: recording.ContentType,
            enableRangeProcessing: true,
            cancellation: ct);
    }
}
