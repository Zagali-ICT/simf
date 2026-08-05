// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// themes, halls, meeting tables, hall allocations, business meetings
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
    // -- D-134 Sprint B — Themes CRUD (D-135 freeze-lift) --------------------

    /// <summary>One page of themes (D-134 Sprint B).</summary>
    public Task<ApiCallResult<GridPage<AdminThemeSummary>>> ListThemesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminThemeSummary>>(
            HttpMethod.Post, "themes/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One theme by id (D-134 Sprint B).</summary>
    public Task<ApiCallResult<AdminThemeDetail>> GetThemeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminThemeDetail>(
            HttpMethod.Get, $"themes/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates a theme (D-134 Sprint B).</summary>
    public Task<ApiCallResult<AdminThemeDetail>> CreateThemeAsync(
        AdminCreateThemeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminThemeDetail>(
            HttpMethod.Post, "themes",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Updates a theme (D-134 Sprint B).</summary>
    public Task<ApiCallResult<AdminThemeDetail>> UpdateThemeAsync(
        Guid id, AdminUpdateThemeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminThemeDetail>(
            HttpMethod.Put, $"themes/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes (deactivates) a theme (D-134 Sprint B).</summary>
    public Task<ApiCallResult<bool>> DeactivateThemeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"themes/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-134 Sprint B — Halls CRUD ----------------------------------------

    public Task<ApiCallResult<GridPage<AdminHallSummary>>> ListHallsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminHallSummary>>(
            HttpMethod.Post, "halls/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallDetail>> GetHallAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallDetail>(
            HttpMethod.Get, $"halls/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallDetail>> CreateHallAsync(
        AdminCreateHallRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallDetail>(
            HttpMethod.Post, "halls",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallDetail>> UpdateHallAsync(
        Guid id, AdminUpdateHallRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallDetail>(
            HttpMethod.Put, $"halls/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateHallAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"halls/{id}", content: null,
            accessToken, cancellationToken);

    // QA B16 — the hall's occupancy view: the sessions assigned to this hall.
    public Task<ApiCallResult<GridPage<AdminSessionSummary>>> GetHallScheduleAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionSummary>>(
            HttpMethod.Get, $"halls/{id}/schedule", content: null,
            accessToken, cancellationToken);

    // -- SIMF-FDS-013 (D-248) — meeting tables + hall allocations + meetings -

    public Task<ApiCallResult<bool>> SetHallPurposeAsync(
        Guid hallId, SetHallPurposeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"halls/{hallId}/purpose",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<MeetingTableRow>>> ListMeetingTablesAsync(
        Guid hallId, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<MeetingTableRow>>(
            HttpMethod.Post, $"halls/{hallId}/meeting-tables/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<MeetingTableRow>> CreateMeetingTableAsync(
        Guid hallId, CreateMeetingTableRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeetingTableRow>(
            HttpMethod.Post, $"halls/{hallId}/meeting-tables",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<MeetingTableRow>> UpdateMeetingTableAsync(
        Guid id, UpdateMeetingTableRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeetingTableRow>(
            HttpMethod.Put, $"meeting-tables/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteMeetingTableAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"meeting-tables/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<MeetingTablesGenerated>> GenerateMeetingTablesAsync(
        Guid hallId, GenerateMeetingTablesRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeetingTablesGenerated>(
            HttpMethod.Post, $"halls/{hallId}/meeting-tables/generate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<HallAllocationRow>>> ListHallAllocationsAsync(
        Guid hallId, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<HallAllocationRow>>(
            HttpMethod.Post, $"halls/{hallId}/hall-allocations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<HallAllocationRow>> CreateHallAllocationAsync(
        Guid hallId, CreateHallAllocationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallAllocationRow>(
            HttpMethod.Post, $"halls/{hallId}/hall-allocations",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> ReleaseHallAllocationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"hall-allocations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<BusinessMeetingRow>>> ListBusinessMeetingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<BusinessMeetingRow>>(
            HttpMethod.Post, "business-meetings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<BusinessMeetingDetail>> GetBusinessMeetingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessMeetingDetail>(
            HttpMethod.Get, $"business-meetings/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<BusinessMeetingScheduled>> ScheduleBusinessMeetingAsync(
        ScheduleMeetingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessMeetingScheduled>(
            HttpMethod.Post, "business-meetings",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> CancelBusinessMeetingAsync(
        Guid id, CancelMeetingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"business-meetings/{id}/cancel",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);
}
