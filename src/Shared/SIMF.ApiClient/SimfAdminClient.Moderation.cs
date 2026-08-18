// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// session-question moderation
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

namespace SIMF.ApiClient;

public sealed partial class SimfAdminClient
{
    // -- Session-question moderation ----------------------------------------

    public Task<ApiCallResult<GridPage<AdminSessionModeratorRow>>> ListSessionModeratorsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionModeratorRow>>(
            HttpMethod.Post, "session-moderators/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    // DEF-MOD-005 — the assign dialog's session + eligible-moderator pickers.
    public Task<ApiCallResult<SessionModeratorAssignOptions>> ListSessionModeratorAssignOptionsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SessionModeratorAssignOptions>(
            HttpMethod.Get, "session-moderators/assign-options", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionModeratorRow>> AssignSessionModeratorAsync(
        AssignSessionModeratorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionModeratorRow>(
            HttpMethod.Post, "session-moderators",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> RevokeSessionModeratorAsync(
        Guid sessionId, Guid userId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"session-moderators/{sessionId}/{userId}", content: null,
            accessToken, cancellationToken);

    // Scientific-Committee Q&A queue (admin base, /admin/questions/*). Server-paged
    // on the shared grid seam: the status and session that used to be query-string
    // parameters are grid filter keys on the GridQuery body now.
    public Task<ApiCallResult<GridPage<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>>
        ListQuestionQueueAsync(
            GridQuery query, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>(
            HttpMethod.Post, "questions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>
        ApproveQuestionAsync(Guid questionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.SessionQuestionQueueRow>(
            HttpMethod.Put, $"questions/{questionId}/approve", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>
        HideQuestionFromQueueAsync(Guid questionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.SessionQuestionQueueRow>(
            HttpMethod.Put, $"questions/{questionId}/hide", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>
        EscalateQuestionAsync(Guid questionId, SIMF.Contracts.Sessions.EscalateQuestionRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.SessionQuestionQueueRow>(
            HttpMethod.Put, $"questions/{questionId}/escalate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // AI session-summary / محضر committee desk (/admin/session-summaries/*).
    // Server-paged on the shared grid seam.
    public Task<ApiCallResult<GridPage<AdminSessionSummaryRow>>>
        ListSessionSummariesAsync(
            GridQuery query, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionSummaryRow>>(
            HttpMethod.Post, "session-summaries/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        GetSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Get, $"session-summaries/{sessionId}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        GenerateSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Post, $"session-summaries/{sessionId}/generate", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        SaveSessionSummaryAsync(Guid sessionId, SaveSessionSummaryRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        PublishSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/publish", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        UnpublishSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/unpublish", content: null,
            accessToken, cancellationToken);

    // The team review/approval workflow.
    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        SubmitSessionSummaryForReviewAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/submit-review", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        ApproveSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/approve", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        ReturnSessionSummaryToDraftAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/return-to-draft", content: null,
            accessToken, cancellationToken);

    // Operator hall-door QR arrival (/admin/sessions/{id}/arrivals).
    public Task<ApiCallResult<SIMF.Contracts.Sessions.QrArrivalResult>>
        RecordQrArrivalAsync(Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.QrArrivalResult>(
            HttpMethod.Post, $"sessions/{sessionId}/arrivals",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // 2026-07-18: operator hall-door QR departure / check-out (/admin/sessions/{id}/departures).
    public Task<ApiCallResult<SIMF.Contracts.Sessions.QrArrivalResult>>
        RecordQrDepartureAsync(Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.QrArrivalResult>(
            HttpMethod.Post, $"sessions/{sessionId}/departures",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>>
        ListModeratorQueueAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendSessionsAsync<IReadOnlyList<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>(
            HttpMethod.Get, $"{sessionId}/questions/moderate", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>
        HideQuestionAsync(Guid sessionId, Guid questionId, bool isHidden,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendSessionsAsync<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>(
            HttpMethod.Put, $"{sessionId}/questions/{questionId}/hide",
            JsonContent.Create(new { isHidden }, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>
        PushQuestionAsync(Guid sessionId, Guid questionId,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendSessionsAsync<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>(
            HttpMethod.Put, $"{sessionId}/questions/{questionId}/push",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> ReorderQuestionsAsync(
        Guid sessionId, IReadOnlyList<Guid> orderedIds, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendSessionsAsync<bool>(
            HttpMethod.Put, $"{sessionId}/questions/reorder",
            JsonContent.Create(new { orderedQuestionIds = orderedIds }, options: JsonOptions),
            accessToken, cancellationToken);
}
