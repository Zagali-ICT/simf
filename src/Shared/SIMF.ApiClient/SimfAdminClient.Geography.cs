// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// countries, regions, contact inquiries, site settings
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
    // -- Country admin lookup CRUD ------------------------------------------

    public Task<ApiCallResult<GridPage<AdminCountrySummary>>> ListCountriesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminCountrySummary>>(
            HttpMethod.Post, "countries/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminCountryDetail>> GetCountryAsync(
        int id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCountryDetail>(
            HttpMethod.Get, $"countries/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminCountryDetail>> CreateCountryAsync(
        AdminCreateCountryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCountryDetail>(
            HttpMethod.Post, "countries",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminCountryDetail>> UpdateCountryAsync(
        int id, AdminUpdateCountryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCountryDetail>(
            HttpMethod.Put, $"countries/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateCountryAsync(
        int id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"countries/{id}", content: null,
            accessToken, cancellationToken);

    // -- Region admin lookup CRUD (mirrors the Country block; Guid key) --------

    public Task<ApiCallResult<GridPage<AdminRegionSummary>>> ListRegionsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRegionSummary>>(
            HttpMethod.Post, "regions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRegionDetail>> GetRegionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRegionDetail>(
            HttpMethod.Get, $"regions/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRegionDetail>> CreateRegionAsync(
        CreateRegionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRegionDetail>(
            HttpMethod.Post, "regions",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRegionDetail>> UpdateRegionAsync(
        Guid id, UpdateRegionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRegionDetail>(
            HttpMethod.Put, $"regions/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateRegionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"regions/{id}", content: null,
            accessToken, cancellationToken);

    // -- Contact-inquiries inbox + Site-settings + Country delegates ----------
    //    (the pages + API shipped first; this block is the CP client/BFF
    //    wiring they were missing). --

    public Task<ApiCallResult<GridPage<AdminContactInquiryRow>>> ListContactInquiriesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminContactInquiryRow>>(
            HttpMethod.Post, "contact-inquiries/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> MarkContactInquiryHandledAsync(
        Guid id, bool handled, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"contact-inquiries/{id}/handled",
            JsonContent.Create(new { handled }, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<SiteSettingsResponse>> GetSiteSettingsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SiteSettingsResponse>(
            HttpMethod.Get, "site-settings", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SiteSettingsResponse>> UpdateSiteSettingsAsync(
        AdminUpdateSiteSettingsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SiteSettingsResponse>(
            HttpMethod.Put, "site-settings",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<WalkInModeSettingsResponse>> GetWalkInModeAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<WalkInModeSettingsResponse>(
            HttpMethod.Get, "walk-in-mode", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<WalkInDeskModeResponse>> GetDeskWalkInModeAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<WalkInDeskModeResponse>(
            HttpMethod.Get, "walk-in-mode/desk", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<WalkInModeSettingsResponse>> UpdateWalkInModeAsync(
        AdminUpdateWalkInModeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<WalkInModeSettingsResponse>(
            HttpMethod.Post, "walk-in-mode",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<AdminCountryDelegateOption>>> ListCountryDelegatesAsync(
        int countryId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminCountryDelegateOption>>(
            HttpMethod.Get, $"countries/{countryId}/delegates", content: null,
            accessToken, cancellationToken);
}
