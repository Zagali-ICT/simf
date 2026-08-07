// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// invitations, VIPs, broadcasts, the content CMS
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
    // -- Public-relations: invitations + VIPs -------------------------------

    public Task<ApiCallResult<GridPage<AdminInvitationSummary>>> ListInvitationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminInvitationSummary>>(
            HttpMethod.Post, "invitations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminInvitationDetail>> GetInvitationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInvitationDetail>(
            HttpMethod.Get, $"invitations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminInvitationDetail>> CreateInvitationAsync(
        AdminCreateInvitationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInvitationDetail>(
            HttpMethod.Post, "invitations",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminInvitationDetail>> UpdateInvitationAsync(
        Guid id, AdminUpdateInvitationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInvitationDetail>(
            HttpMethod.Put, $"invitations/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateInvitationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"invitations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminVipSummary>>> ListVipsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminVipSummary>>(
            HttpMethod.Post, "vips/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNotifyVipsResult>> NotifyVipsAsync(
        AdminNotifyVipsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNotifyVipsResult>(
            HttpMethod.Post, "vips/notify",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- Notification broadcasts (Control Panel "Announcements" desk) --------

    public Task<ApiCallResult<AdminBroadcastResult>> CreateBroadcastAsync(
        AdminCreateBroadcastRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBroadcastResult>(
            HttpMethod.Post, "notifications/broadcast",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBroadcastEstimateResult>> EstimateBroadcastAsync(
        AdminBroadcastEstimateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBroadcastEstimateResult>(
            HttpMethod.Post, "notifications/broadcast/estimate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminBroadcastSummary>>> ListBroadcastsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBroadcastSummary>>(
            HttpMethod.Post, "notifications/broadcasts/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBroadcastSummary>> GetBroadcastAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBroadcastSummary>(
            HttpMethod.Get, $"notifications/broadcasts/{id}", content: null,
            accessToken, cancellationToken);

    // -- Dynamic content CMS ------------------------------------------------

    public Task<ApiCallResult<GridPage<AdminContentBlockSummary>>> ListContentBlocksAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminContentBlockSummary>>(
            HttpMethod.Post, "content-blocks/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContentBlockSummary>> GetContentBlockAsync(
        string key, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContentBlockSummary>(
            HttpMethod.Get, $"content-blocks/{Uri.EscapeDataString(key)}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContentBlockSummary>> UpsertContentBlockAsync(
        UpsertContentBlockRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContentBlockSummary>(
            HttpMethod.Put, "content-blocks",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteContentBlockAsync(
        string key, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"content-blocks/{Uri.EscapeDataString(key)}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminBannerSummary>>> ListBannersAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBannerSummary>>(
            HttpMethod.Post, "banners/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBannerDetail>> GetBannerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBannerDetail>(
            HttpMethod.Get, $"banners/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBannerDetail>> CreateBannerAsync(
        CreateBannerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBannerDetail>(
            HttpMethod.Post, "banners",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBannerDetail>> UpdateBannerAsync(
        Guid id, UpdateBannerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBannerDetail>(
            HttpMethod.Put, $"banners/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteBannerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"banners/{id}", content: null,
            accessToken, cancellationToken);
}
