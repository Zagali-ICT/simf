// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// speakers, sessions, the registration gate, archive visibility
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Requests;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Email;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Logs;
using SIMF.Contracts.Media;
using SIMF.Contracts.Organization;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Statistics;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Ops;
using SIMF.Contracts.Support;
using SIMF.Common.Enums;

namespace SIMF.ApiClient;

public sealed partial class SimfAdminClient
{
    // -- Speaker admin CRUD -------------------------------------------------

    public Task<ApiCallResult<GridPage<AdminSpeakerSummary>>> ListSpeakersAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSpeakerSummary>>(
            HttpMethod.Post, "speakers/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerDetail>> GetSpeakerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerDetail>(
            HttpMethod.Get, $"speakers/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerDetail>> CreateSpeakerAsync(
        AdminCreateSpeakerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerDetail>(
            HttpMethod.Post, "speakers",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerDetail>> UpdateSpeakerAsync(
        Guid id, AdminUpdateSpeakerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerDetail>(
            HttpMethod.Put, $"speakers/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSpeakerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"speakers/{id}", content: null,
            accessToken, cancellationToken);

    // -- Session admin CRUD --------------------------------------------------

    // NOTE: returns AdminSessionSummary (the sessions GRID row — Code/Title/Hall/
    // times/capacity/status), NOT AdminSessionSummaryDetail (the AI session-summary
    // detail). The two names collide and the wrong one was bound here, which made
    // /admin/sessions deserialize every row to defaults (blank code, 0001 dates,
    // inactive). The CP SessionsList grid is TItem="AdminSessionSummary".
    public Task<ApiCallResult<GridPage<AdminSessionSummary>>> ListSessionsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionSummary>>(
            HttpMethod.Post, "sessions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionDetail>> GetSessionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Get, $"sessions/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionDetail>> CreateSessionAsync(
        AdminCreateSessionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Post, "sessions",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionDetail>> UpdateSessionAsync(
        Guid id, AdminUpdateSessionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Put, $"sessions/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSessionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"sessions/{id}", content: null,
            accessToken, cancellationToken);

    // Server-side subtitle fetch from a session's video (YouTube).
    public Task<ApiCallResult<FetchSubtitleResponse>> FetchSessionSubtitleAsync(
        FetchSubtitleRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<FetchSubtitleResponse>(
            HttpMethod.Post, "sessions/subtitle/fetch-from-video",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Session broadcast-lifecycle transition.
    public Task<ApiCallResult<AdminSessionDetail>> SetSessionStatusAsync(
        Guid id, SetSessionStatusRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Put, $"sessions/{id}/status",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Attach the session recording. The body is STREAMED (not a
    // byte[]) so a large video is not buffered whole in the CP — the caller's
    // stream is forwarded straight through to the API.
    public Task<ApiCallResult<AdminSessionDetail>> UploadSessionRecordingAsync(
        Guid id, Stream content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AdminSessionDetail>(
            HttpMethod.Post, $"sessions/{id}/recording",
            multipart, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<AdminSessionDetail>> DeleteSessionRecordingAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Delete, $"sessions/{id}/recording", content: null,
            accessToken, cancellationToken);

    // -- Registration gate + archive visibility ----------------------------

    public Task<ApiCallResult<RegistrationGateState>> GetRegistrationGateAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<RegistrationGateState>(
            HttpMethod.Get, "registration-gate", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<RegistrationGateState>> UpdateRegistrationGateAsync(
        UpdateRegistrationGateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<RegistrationGateState>(
            HttpMethod.Put, "registration-gate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<ArchiveVisibilityState>> GetArchiveVisibilityAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<ArchiveVisibilityState>(
            HttpMethod.Get, "archive/visibility", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<ArchiveVisibilityState>> UpdateArchiveVisibilityAsync(
        UpdateArchiveVisibilityRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<ArchiveVisibilityState>(
            HttpMethod.Put, "archive/visibility",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);
}
