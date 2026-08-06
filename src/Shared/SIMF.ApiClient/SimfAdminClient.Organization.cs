// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// the organisation / about profile editor
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
    // -- D-495 — Organization / About profile editor --------------------------

    /// <summary>Read the full Organization Profile (incl. child-row ids)
    /// for the CP editor. Gated by OrganizationProfile.View.</summary>
    public Task<ApiCallResult<OrganizationProfileResponse>> GetOrganizationProfileAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationProfileResponse>(
            HttpMethod.Get, "organization-profile", null, accessToken, cancellationToken);

    /// <summary>Save the Organization Profile (full-document upsert).
    /// Gated by OrganizationProfile.Manage.</summary>
    public Task<ApiCallResult<OrganizationProfileResponse>> SaveOrganizationProfileAsync(
        AdminUpdateOrganizationProfileRequest request,
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationProfileResponse>(
            HttpMethod.Put, "organization-profile",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Upload (replace) the hero background video. STREAMED (not a
    /// byte[]) so a large video is not buffered whole in the CP — the caller's stream
    /// forwards straight to the API. Gated by OrganizationProfile.Manage.</summary>
    public Task<ApiCallResult<OrganizationProfileResponse>> UploadOrganizationHeroVideoAsync(
        Stream content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<OrganizationProfileResponse>(
            HttpMethod.Post, "organization-profile/hero-video",
            multipart, accessToken, cancellationToken);
    }

    /// <summary>Remove the uploaded hero background video (reverts the hero to
    /// the banner image). Gated by OrganizationProfile.Manage.</summary>
    public Task<ApiCallResult<OrganizationProfileResponse>> DeleteOrganizationHeroVideoAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationProfileResponse>(
            HttpMethod.Delete, "organization-profile/hero-video", content: null,
            accessToken, cancellationToken);
}
