// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// system configuration, the venue map, archive editions
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
    // -- P2.4 (D-229) — System Configuration settings (SIMF.Contracts.Admin) -

    public Task<ApiCallResult<GridPage<AdminSystemSettingSummary>>> ListSystemSettingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSystemSettingSummary>>(
            HttpMethod.Post, "system-settings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSystemSettingDetail>> GetSystemSettingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSystemSettingDetail>(
            HttpMethod.Get, $"system-settings/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSystemSettingDetail>> CreateSystemSettingAsync(
        AdminCreateSystemSettingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSystemSettingDetail>(
            HttpMethod.Post, "system-settings",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSystemSettingDetail>> UpdateSystemSettingAsync(
        Guid id, AdminUpdateSystemSettingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSystemSettingDetail>(
            HttpMethod.Put, $"system-settings/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteSystemSettingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"system-settings/{id}", content: null,
            accessToken, cancellationToken);

    // -- P2.5 (D-230) — 2D venue-map node CRUD (SIMF.Contracts.Admin) --------

    public Task<ApiCallResult<GridPage<AdminVenueMapNodeSummary>>> ListVenueMapNodesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminVenueMapNodeSummary>>(
            HttpMethod.Post, "venue-map/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminVenueMapNodeDetail>> GetVenueMapNodeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminVenueMapNodeDetail>(
            HttpMethod.Get, $"venue-map/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminVenueMapNodeDetail>> CreateVenueMapNodeAsync(
        AdminCreateVenueMapNodeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminVenueMapNodeDetail>(
            HttpMethod.Post, "venue-map",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminVenueMapNodeDetail>> UpdateVenueMapNodeAsync(
        Guid id, AdminUpdateVenueMapNodeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminVenueMapNodeDetail>(
            HttpMethod.Put, $"venue-map/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteVenueMapNodeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"venue-map/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Archive edition admin CRUD (SIMF.Contracts.Archive) --------

    public Task<ApiCallResult<GridPage<AdminArchiveEditionSummary>>> ListArchiveEditionsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminArchiveEditionSummary>>(
            HttpMethod.Post, "archive/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> GetArchiveEditionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Get, $"archive/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> CreateArchiveEditionAsync(
        CreateArchiveEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Post, "archive",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> UpdateArchiveEditionAsync(
        Guid id, UpdateArchiveEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Put, $"archive/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteArchiveEditionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"archive/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> SnapshotCurrentArchiveEditionAsync(
        SnapshotCurrentEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Post, "archive/snapshot-current",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);
}
