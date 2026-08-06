// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// news, media, assets, media partners, sponsors, booths
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
    // -- D-199 — News admin CRUD (SIMF.Contracts.PublicRelations) -----------

    public Task<ApiCallResult<GridPage<AdminNewsSummary>>> ListNewsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminNewsSummary>>(
            HttpMethod.Post, "news/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNewsDetail>> GetNewsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNewsDetail>(
            HttpMethod.Get, $"news/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNewsDetail>> CreateNewsAsync(
        CreateNewsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNewsDetail>(
            HttpMethod.Post, "news",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNewsDetail>> UpdateNewsAsync(
        Guid id, UpdateNewsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNewsDetail>(
            HttpMethod.Put, $"news/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteNewsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"news/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Media gallery admin CRUD (SIMF.Contracts.Media) ------------

    public Task<ApiCallResult<GridPage<AdminMediaSummary>>> ListMediaAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminMediaSummary>>(
            HttpMethod.Post, "media/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaDetail>> GetMediaAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaDetail>(
            HttpMethod.Get, $"media/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaDetail>> CreateMediaAsync(
        AdminCreateMediaRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaDetail>(
            HttpMethod.Post, "media",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaDetail>> UpdateMediaAsync(
        Guid id, AdminUpdateMediaRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaDetail>(
            HttpMethod.Put, $"media/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteMediaAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"media/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Multipart upload of a media item's primary image
    /// (models <see cref="UploadVisitorIdDocumentAsync"/>).</summary>
    public Task<ApiCallResult<AdminMediaDetail>> UploadMediaImageAsync(
        Guid id, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AdminMediaDetail>(
            HttpMethod.Post, $"media/{id}/image", multipart,
            accessToken, cancellationToken);
    }

    // -- D-357 — unified media-asset pipeline (one upload / link / fetch for every entity) --

    /// <summary>Upload (or replace) a media asset's file for (category, owner).</summary>
    public Task<ApiCallResult<bool>> UploadAssetImageAsync(
        string category, Guid ownerId, string kind,
        byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        multipart.Add(new StringContent(kind), "kind");
        return SendAsync<bool>(
            HttpMethod.Post, $"assets/{category}/{ownerId}/image", multipart,
            accessToken, cancellationToken);
    }

    /// <summary>Set (or replace) a media asset to an external link for (category, owner).</summary>
    public Task<ApiCallResult<bool>> SetAssetLinkAsync(
        string category, Guid ownerId,
        SIMF.Contracts.Assets.SetAssetLinkRequest request,
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"assets/{category}/{ownerId}/link",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Fetch an asset's bytes for the CP admin preview proxy (the API
    /// 302s an external-link asset, which HttpClient follows transparently).</summary>
    public async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchAssetImageAsync(
        string category, Guid ownerId, string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}assets/{category}/{ownerId}/image");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) { return ((int)response.StatusCode, null, []); }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType, bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return (503, null, []);
        }
    }

    /// <summary>One page of all media assets for the central Media Library.</summary>
    public Task<ApiCallResult<GridPage<SIMF.Contracts.Assets.AdminAssetSummary>>> ListAssetsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<SIMF.Contracts.Assets.AdminAssetSummary>>(
            HttpMethod.Post, "assets/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One media asset by id.</summary>
    public Task<ApiCallResult<SIMF.Contracts.Assets.AdminAssetSummary>> GetAssetAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Assets.AdminAssetSummary>(
            HttpMethod.Get, $"assets/item/{id}", null, accessToken, cancellationToken);

    /// <summary>Soft-delete (deactivate) a media asset.</summary>
    public Task<ApiCallResult<bool>> DeactivateAssetAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"assets/item/{id}", null, accessToken, cancellationToken);

    /// <summary>Restore a soft-deleted media asset.</summary>
    public Task<ApiCallResult<bool>> RestoreAssetAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Post, $"assets/item/{id}/restore", null, accessToken, cancellationToken);

    // -- D-199 — Media-partner admin CRUD (SIMF.Contracts.PublicRelations) --

    public Task<ApiCallResult<GridPage<AdminMediaPartnerSummary>>> ListMediaPartnersAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminMediaPartnerSummary>>(
            HttpMethod.Post, "media-partners/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaPartnerDetail>> CreateMediaPartnerAsync(
        AdminCreateMediaPartnerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaPartnerDetail>(
            HttpMethod.Post, "media-partners",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaPartnerDetail>> UpdateMediaPartnerAsync(
        Guid id, AdminUpdateMediaPartnerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaPartnerDetail>(
            HttpMethod.Put, $"media-partners/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateMediaPartnerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"media-partners/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Sponsor admin CRUD (SIMF.Contracts.Admin) ------------------

    public Task<ApiCallResult<GridPage<AdminSponsorSummary>>> ListSponsorsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSponsorSummary>>(
            HttpMethod.Post, "sponsors/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSponsorDetail>> CreateSponsorAsync(
        AdminCreateSponsorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSponsorDetail>(
            HttpMethod.Post, "sponsors",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSponsorDetail>> UpdateSponsorAsync(
        Guid id, AdminUpdateSponsorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSponsorDetail>(
            HttpMethod.Put, $"sponsors/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSponsorAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"sponsors/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Booth admin CRUD (SIMF.Contracts.Exhibition) ---------------

    public Task<ApiCallResult<GridPage<AdminBoothSummary>>> ListBoothsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBoothSummary>>(
            HttpMethod.Post, "booths/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBoothDetail>> GetBoothAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBoothDetail>(
            HttpMethod.Get, $"booths/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBoothDetail>> CreateBoothAsync(
        AdminCreateBoothRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBoothDetail>(
            HttpMethod.Post, "booths",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBoothDetail>> UpdateBoothAsync(
        Guid id, AdminUpdateBoothRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBoothDetail>(
            HttpMethod.Put, $"booths/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateBoothAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"booths/{id}", content: null,
            accessToken, cancellationToken);
}
