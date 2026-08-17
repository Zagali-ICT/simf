// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// the AI module and transactional e-mail templates
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
    // -- Centralised AI module ---------------------------------------------

    public Task<ApiCallResult<GridPage<AdminAiPromptSummary>>> ListAiPromptsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminAiPromptSummary>>(
            HttpMethod.Post, "ai/prompts/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminAiPromptDetail>> GetAiPromptAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiPromptDetail>(
            HttpMethod.Get, $"ai/prompts/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>One page of the append-only edit history for one prompt.
    /// Newest version first. Empty page when the prompt has never been
    /// updated past v1.</summary>
    public Task<ApiCallResult<GridPage<AdminAiPromptHistoryEntry>>> ListAiPromptHistoryAsync(
        Guid id, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminAiPromptHistoryEntry>>(
            HttpMethod.Post, $"ai/prompts/{id}/history/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminAiPromptDetail>> CreateAiPromptAsync(
        CreateAiPromptRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiPromptDetail>(
            HttpMethod.Post, "ai/prompts",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminAiPromptDetail>> UpdateAiPromptAsync(
        Guid id, UpdateAiPromptRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiPromptDetail>(
            HttpMethod.Put, $"ai/prompts/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateAiPromptAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"ai/prompts/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AiCallResult>> TestAiPromptAsync(
        Guid id, TestAiPromptRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AiCallResult>(
            HttpMethod.Post, $"ai/prompts/{id}/test",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminAiInvocationRow>>> ListAiInvocationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminAiInvocationRow>>(
            HttpMethod.Post, "ai/invocations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>The Control Panel operator assistant — sends the operator's
    /// question plus the grounding page directory to the <c>cp-assistant</c>
    /// prompt and returns the answer.</summary>
    public Task<ApiCallResult<AiCallResult>> AssistAsync(
        CpAssistantRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AiCallResult>(
            HttpMethod.Post, "ai/assistant",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Full redacted payload for one invocation (SOC drill-down).</summary>
    public Task<ApiCallResult<AdminAiInvocationDetail>> GetAiInvocationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiInvocationDetail>(
            HttpMethod.Get, $"ai/invocations/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>CP Phase-1 — the AI dashboard 24h health aggregate.</summary>
    public Task<ApiCallResult<AdminAiDashboard>> GetAiDashboardAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiDashboard>(
            HttpMethod.Get, "ai/dashboard", content: null,
            accessToken, cancellationToken);

    // -- Transactional email templates (list / read / edit / reset /
    //    preview). The {type} segment is the EmailTemplateType name (the DB holds
    //    only overrides; the catalogue backs every read so the grid shows all six).

    public Task<ApiCallResult<GridPage<AdminEmailTemplateSummary>>> ListEmailTemplatesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminEmailTemplateSummary>>(
            HttpMethod.Post, "email/templates/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminEmailTemplateDetail>> GetEmailTemplateAsync(
        string type, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminEmailTemplateDetail>(
            HttpMethod.Get, $"email/templates/{type}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminEmailTemplateDetail>> UpdateEmailTemplateAsync(
        string type, UpdateEmailTemplateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminEmailTemplateDetail>(
            HttpMethod.Put, $"email/templates/{type}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminEmailTemplateDetail>> ResetEmailTemplateAsync(
        string type, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminEmailTemplateDetail>(
            HttpMethod.Post, $"email/templates/{type}/reset", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<EmailTemplatePreviewResult>> PreviewEmailTemplateAsync(
        string type, PreviewEmailTemplateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<EmailTemplatePreviewResult>(
            HttpMethod.Post, $"email/templates/{type}/preview",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);
}
