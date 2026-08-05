// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// interests, FAQ, rating configuration
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
    // -- P9 — Interests CRUD (الاهتمامات) -----------------------------------

    /// <summary>One page of interests for the admin grid (P9 — D-050).</summary>
    public Task<ApiCallResult<GridPage<AdminInterestSummary>>> ListInterestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminInterestSummary>>(
            HttpMethod.Post, "interests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One interest by id (P9 — D-050).</summary>
    public Task<ApiCallResult<AdminInterestSummary>> GetInterestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInterestSummary>(
            HttpMethod.Get, $"interests/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates an interest (P9 — D-050).</summary>
    public Task<ApiCallResult<AdminInterestSummary>> CreateInterestAsync(
        AdminCreateInterestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInterestSummary>(
            HttpMethod.Post, "interests",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Updates an interest (P9 — D-050).</summary>
    public Task<ApiCallResult<AdminInterestSummary>> UpdateInterestAsync(
        Guid id, AdminUpdateInterestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInterestSummary>(
            HttpMethod.Put, $"interests/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes (deactivates) an interest (P9 — D-050).</summary>
    public Task<ApiCallResult<bool>> DeactivateInterestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"interests/{id}", content: null,
            accessToken, cancellationToken);

    // -- P2.1 (D-211) — FAQ management (two-level group → entry) --

    public Task<ApiCallResult<GridPage<AdminFaqGroupSummary>>> ListFaqGroupsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminFaqGroupSummary>>(HttpMethod.Post, "faq/groups/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqGroupSummary>> GetFaqGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqGroupSummary>(HttpMethod.Get, $"faq/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqGroupSummary>> CreateFaqGroupAsync(
        CreateFaqGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqGroupSummary>(HttpMethod.Post, "faq/groups",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqGroupSummary>> UpdateFaqGroupAsync(
        Guid id, UpdateFaqGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqGroupSummary>(HttpMethod.Put, $"faq/groups/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteFaqGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"faq/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminFaqEntrySummary>>> ListFaqEntriesAsync(
        Guid groupId, GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminFaqEntrySummary>>(HttpMethod.Post, $"faq/groups/{groupId}/entries/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqEntrySummary>> CreateFaqEntryAsync(
        CreateFaqEntryRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqEntrySummary>(HttpMethod.Post, "faq/entries",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqEntrySummary>> UpdateFaqEntryAsync(
        Guid id, UpdateFaqEntryRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqEntrySummary>(HttpMethod.Put, $"faq/entries/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteFaqEntryAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"faq/entries/{id}", content: null,
            accessToken, cancellationToken);

    // -- Rating configuration (types → groups → questions) ------------------

    public Task<ApiCallResult<GridPage<AdminRatingTypeSummary>>> ListRatingTypesAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRatingTypeSummary>>(HttpMethod.Post, "ratings/types/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingTypeSummary>> GetRatingTypeAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingTypeSummary>(HttpMethod.Get, $"ratings/types/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingTypeSummary>> CreateRatingTypeAsync(
        CreateRatingTypeRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingTypeSummary>(HttpMethod.Post, "ratings/types",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingTypeSummary>> UpdateRatingTypeAsync(
        Guid id, UpdateRatingTypeRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingTypeSummary>(HttpMethod.Put, $"ratings/types/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteRatingTypeAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"ratings/types/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminRatingQuestionGroupSummary>>> ListRatingGroupsAsync(
        Guid typeId, GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRatingQuestionGroupSummary>>(HttpMethod.Post, $"ratings/types/{typeId}/groups/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionGroupSummary>> GetRatingGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionGroupSummary>(HttpMethod.Get, $"ratings/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionGroupSummary>> CreateRatingGroupAsync(
        CreateRatingQuestionGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionGroupSummary>(HttpMethod.Post, "ratings/groups",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionGroupSummary>> UpdateRatingGroupAsync(
        Guid id, UpdateRatingQuestionGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionGroupSummary>(HttpMethod.Put, $"ratings/groups/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteRatingGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"ratings/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminRatingQuestionSummary>>> ListRatingQuestionsAsync(
        Guid typeId, GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRatingQuestionSummary>>(HttpMethod.Post, $"ratings/types/{typeId}/questions/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionSummary>> GetRatingQuestionAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionSummary>(HttpMethod.Get, $"ratings/questions/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionSummary>> CreateRatingQuestionAsync(
        CreateRatingQuestionRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionSummary>(HttpMethod.Post, "ratings/questions",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionSummary>> UpdateRatingQuestionAsync(
        Guid id, UpdateRatingQuestionRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionSummary>(HttpMethod.Put, $"ratings/questions/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteRatingQuestionAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"ratings/questions/{id}", content: null,
            accessToken, cancellationToken);
}
