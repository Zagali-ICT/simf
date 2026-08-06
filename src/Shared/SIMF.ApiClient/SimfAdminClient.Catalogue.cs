// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// organisations, session categories, programme days, presentations
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
    // -- B3 (D-220) — Organisation lookup admin CRUD + gov-Excel import
    //    (SIMF.Contracts.Organisations) ------------------------------------

    public Task<ApiCallResult<GridPage<AdminOrganisationSummary>>> ListOrganisationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminOrganisationSummary>>(
            HttpMethod.Post, "organisations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminOrganisationDetail>> GetOrganisationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOrganisationDetail>(
            HttpMethod.Get, $"organisations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminOrganisationDetail>> CreateOrganisationAsync(
        CreateOrganisationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOrganisationDetail>(
            HttpMethod.Post, "organisations",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminOrganisationDetail>> UpdateOrganisationAsync(
        Guid id, UpdateOrganisationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOrganisationDetail>(
            HttpMethod.Put, $"organisations/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateOrganisationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"organisations/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Bulk-import a government Excel sheet of Saudi
    /// companies (multipart; form field "file"). Idempotent upsert by CR.</summary>
    public Task<ApiCallResult<OrganisationImportResult>> ImportOrganisationsAsync(
        byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<OrganisationImportResult>(
            HttpMethod.Post, "organisations/import", multipart,
            accessToken, cancellationToken);
    }

    // single-row detail fetch so the Sponsor / MediaPartner edit modals can
    // pre-load the row for editing (their backend GET endpoints already exist;
    // only these client + BFF passthroughs were missing). Mirrors
    // GetOrganisationAsync.
    public Task<ApiCallResult<AdminSponsorDetail>> GetSponsorAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSponsorDetail>(
            HttpMethod.Get, $"sponsors/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaPartnerDetail>> GetMediaPartnerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaPartnerDetail>(
            HttpMethod.Get, $"media-partners/{id}", content: null,
            accessToken, cancellationToken);

    // -- B9b (D-226) — Session-category dynamic lookup admin CRUD
    //    (SIMF.Contracts.Admin) ---------------------------------------------

    public Task<ApiCallResult<GridPage<AdminSessionCategorySummary>>> ListSessionCategoriesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionCategorySummary>>(
            HttpMethod.Post, "session-categories/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionCategoryDetail>> GetSessionCategoryAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionCategoryDetail>(
            HttpMethod.Get, $"session-categories/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionCategoryDetail>> CreateSessionCategoryAsync(
        AdminCreateSessionCategoryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionCategoryDetail>(
            HttpMethod.Post, "session-categories",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionCategoryDetail>> UpdateSessionCategoryAsync(
        Guid id, AdminUpdateSessionCategoryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionCategoryDetail>(
            HttpMethod.Put, $"session-categories/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSessionCategoryAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"session-categories/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-452 — Programme-days admin CRUD (SIMF.Contracts.Admin). Date +
    //    bilingual title; the logo rides the generic asset endpoints
    //    (AssetCategory.ProgrammeDayImage). Mirrors the session-category shape.

    public Task<ApiCallResult<GridPage<AdminProgrammeDaySummary>>> ListProgrammeDaysAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminProgrammeDaySummary>>(
            HttpMethod.Post, "programme-days/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminProgrammeDayDetail>> GetProgrammeDayAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProgrammeDayDetail>(
            HttpMethod.Get, $"programme-days/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminProgrammeDayDetail>> CreateProgrammeDayAsync(
        AdminCreateProgrammeDayRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProgrammeDayDetail>(
            HttpMethod.Post, "programme-days",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminProgrammeDayDetail>> UpdateProgrammeDayAsync(
        Guid id, AdminUpdateProgrammeDayRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProgrammeDayDetail>(
            HttpMethod.Put, $"programme-days/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateProgrammeDayAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"programme-days/{id}", content: null,
            accessToken, cancellationToken);

    // -- #6/#17 — Booking monitor (read-only; SIMF.Contracts.Sessions) --------
    // Bookings auto-confirm (no approval step) and no-shows are released by a
    // background worker, so the CP only reads the active-reservations list.

    public Task<ApiCallResult<GridPage<ActiveBookingRow>>> ListActiveBookingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<ActiveBookingRow>>(
            HttpMethod.Post, "bookings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    // -- P2.3 (D-228) — Speaker presentation files (SIMF.Contracts.Admin) ----

    public Task<ApiCallResult<IReadOnlyList<AdminSpeakerPresentationRow>>> ListSpeakerPresentationsAsync(
        Guid speakerId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminSpeakerPresentationRow>>(
            HttpMethod.Get, $"speakers/{speakerId}/presentations", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerPresentationRow>> UploadSpeakerPresentationAsync(
        Guid speakerId, Guid sessionId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AdminSpeakerPresentationRow>(
            HttpMethod.Post, $"speakers/{speakerId}/presentations?sessionId={sessionId}",
            multipart, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<bool>> DeleteSpeakerPresentationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"speaker-presentations/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>P2.3 — streamed read of a presentation file for the CP download
    /// proxy. Bypasses the <c>ApiResult</c> envelope (binary body); returns the
    /// status, content type, content-disposition and bytes verbatim.</summary>
    public async Task<(int StatusCode, string? ContentType, string? ContentDisposition, byte[] Bytes)>
        FetchSpeakerPresentationAsync(
            Guid id, string accessToken, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}speaker-presentations/{id}/file");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.ToString(),
                response.Content.Headers.ContentDisposition?.ToString(),
                bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, null, []);
        }
    }
}
