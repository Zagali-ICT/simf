using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Cms;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Media;
using SIMF.Contracts.Organization;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Sponsors;

namespace SIMF.ApiClient;

/// <summary>
/// A typed client over the SIMF anonymous public-read endpoints (the public
/// programme and speakers surface, D-199). Unlike <see cref="SimfAuthClient"/>
/// and <see cref="SimfAccountClient"/> it carries no bearer token — the public
/// reads are anonymous. The registered <see cref="HttpClient"/> supplies only
/// the <c>BaseAddress</c> (same as <see cref="SimfAuthClient"/> and
/// <see cref="SimfAccountClient"/>); the backend's anonymous public endpoints
/// do not require an <c>X-App-Key</c> header in this build, so no method sets
/// one per call.
///
/// <para>Every method GETs its route, reads the standard
/// <see cref="ApiResult{T}"/> envelope, and returns the <c>Data</c> payload —
/// or <c>null</c> when the envelope reports failure, when <c>Data</c> is null,
/// or when the service could not be reached. The page renders its own loading,
/// empty, and error states from that null, exactly the way the website's
/// authentication pages branch on the envelope.</para>
/// </summary>
public sealed class SimfPublicClient(HttpClient http)
{
    private const string BasePath = "api/v1/app/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The public agenda — every published session
    /// (<c>GET /api/v1/app/programme/sessions</c>). Returns <c>null</c> on a
    /// failed envelope or an unreachable service.</summary>
    public Task<PublicSessions?> GetProgrammeSessionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicSessions>("programme/sessions", cancellationToken);

    /// <summary>The full public view of one session
    /// (<c>GET /api/v1/app/programme/sessions/{id}</c>). Returns <c>null</c> on a
    /// failed envelope (for example an unknown id) or an unreachable
    /// service.</summary>
    public Task<PublicSessionDetail?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<PublicSessionDetail>($"programme/sessions/{id}", cancellationToken);

    /// <summary>The public speakers list (<c>GET /api/v1/app/speakers</c>).
    /// Returns <c>null</c> on a failed envelope or an unreachable
    /// service.</summary>
    public Task<PublicSpeakers?> GetSpeakersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicSpeakers>("speakers", cancellationToken);

    /// <summary>D-466 — the public, CP-editable site settings
    /// (<c>GET /api/v1/app/site-settings</c>): the registration welcome message +
    /// the social links. Returns <c>null</c> on a failed envelope or an
    /// unreachable service.</summary>
    public Task<SiteSettingsResponse?> GetSiteSettingsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SiteSettingsResponse>("site-settings", cancellationToken);

    /// <summary>D-495 — the public Organization / About profile
    /// (<c>GET /api/v1/app/organization-profile</c>): the edition-generic forum
    /// config (name / dates / status / location / contact / social / about / details).
    /// Returns <c>null</c> on a failed envelope or an unreachable service.</summary>
    public Task<OrganizationProfileResponse?> GetOrganizationProfileAsync(CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationProfileResponse>("organization-profile", cancellationToken);

    /// <summary>One page of the public News feed
    /// (<c>GET /api/v1/app/news</c>). Returns <c>null</c> on a failed envelope
    /// or an unreachable service.</summary>
    public Task<PublicNewsPage?> GetNewsAsync(
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        GetAsync<PublicNewsPage>($"news?page={page}&pageSize={pageSize}", cancellationToken);

    /// <summary>The public Archive / Past-Editions list
    /// (<c>GET /api/v1/app/archive</c>). <c>Items</c> is empty when the
    /// archive-visibility toggle is off; returns <c>null</c> only on a failed
    /// envelope or an unreachable service.</summary>
    public Task<PublicArchive?> GetArchiveAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicArchive>("archive", cancellationToken);

    /// <summary>The public media-partner list
    /// (<c>GET /api/v1/app/media-partners</c>). Returns <c>null</c> on a failed
    /// envelope or an unreachable service.</summary>
    public Task<PublicMediaPartners?> GetMediaPartnersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicMediaPartners>("media-partners", cancellationToken);

    /// <summary>The public sponsors list, grouped by tier
    /// (<c>GET /api/v1/app/sponsors</c>). Returns <c>null</c> on a failed
    /// envelope or an unreachable service.</summary>
    public Task<PublicSponsors?> GetSponsorsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicSponsors>("sponsors", cancellationToken);

    /// <summary>Ask the public FAQ assistant a free-text question
    /// (<c>POST /api/v1/app/ai/faq</c>, anonymous). The answer is grounded on the
    /// forum FAQ by the centralised AI. Returns <c>null</c> on a failed envelope
    /// or an unreachable service; the caller then shows a graceful fallback.</summary>
    public Task<AiCallResult?> AskFaqAsync(string question, CancellationToken cancellationToken = default) =>
        PostAsync<AskFaqRequest, AiCallResult>(
            "ai/faq", new AskFaqRequest { Question = question }, cancellationToken);

    /// <summary>One page of the public media gallery
    /// (<c>GET /api/v1/app/media</c>), optionally narrowed to one album.
    /// Returns <c>null</c> on a failed envelope or an unreachable service.</summary>
    public Task<PublicMediaPage?> GetMediaAsync(
        string? album = null, int skip = 0, int top = 25, CancellationToken cancellationToken = default)
    {
        var query = $"media?skip={skip}&top={top}";
        if (!string.IsNullOrWhiteSpace(album))
        {
            query += $"&album={Uri.EscapeDataString(album)}";
        }
        return GetAsync<PublicMediaPage>(query, cancellationToken);
    }

    /// <summary>The currently-active public banners
    /// (<c>GET /api/v1/app/banners</c>). Returns <c>null</c> on a failed
    /// envelope or an unreachable service.</summary>
    public Task<PublicBanners?> GetBannersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicBanners>("banners", cancellationToken);

    /// <summary>A batch read of CMS content blocks by key
    /// (<c>POST /api/v1/app/content/batch</c>). Missing keys are simply absent
    /// from the returned dictionary. Returns <c>null</c> on a failed envelope or
    /// an unreachable service.</summary>
    public Task<PublicContentBlockBatch?> GetContentBatchAsync(
        IReadOnlyList<string> keys, CancellationToken cancellationToken = default) =>
        PostAsync<PublicContentBlockBatchRequest, PublicContentBlockBatch>(
            "content/batch",
            new PublicContentBlockBatchRequest { Keys = keys.ToList() },
            cancellationToken);

    /// <summary>Fetch one gallery item's primary image bytes
    /// (<c>GET /api/v1/app/media/{id}/image</c>) so a same-origin proxy can
    /// re-stream it to the browser. Returns the upstream status, content type
    /// and bytes; an unreachable service surfaces as 503 with no bytes.</summary>
    public async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchMediaImageAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}media/{id}/image");
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, []);
        }
    }

    /// <summary>D-357 — fetch a unified media asset's bytes
    /// (<c>GET /api/v1/app/assets/{category}/{ownerId}/image</c>) so the Website's
    /// same-origin proxy can re-stream it. An external-link asset is followed
    /// transparently by HttpClient; an unreachable service surfaces as 503.</summary>
    public async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchAssetImageAsync(
        string category, Guid ownerId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}assets/{category}/{ownerId}/image");
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, []);
        }
    }

    /// <summary>Fetch a session presentation file's bytes for the Website's
    /// same-origin download proxy (Session-detail "روابط التحميل", Figma
    /// 5991-85840): <c>GET /app/sessions/{sessionId}/downloads/{presentationId}</c>
    /// (anonymous). Returns the status, content-type, the original file name (from
    /// the attachment header) and the bytes; an unreachable service surfaces as 503.</summary>
    public async Task<(int StatusCode, string? ContentType, string? FileName, byte[] Bytes)>
        FetchSessionDownloadAsync(
            Guid sessionId, Guid presentationId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}sessions/{sessionId}/downloads/{presentationId}");
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var rawName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
            var fileName = string.IsNullOrEmpty(rawName) ? null : Uri.UnescapeDataString(rawName);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                fileName,
                bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, null, []);
        }
    }

    /// <summary>D-717 (item 7, GAP-3) — preview a speaker action link WITHOUT
    /// consuming it (<c>GET /api/v1/app/meeting-actions/{token}</c>). Returns
    /// <c>null</c> for an invalid / expired / used token (the neutral "link no
    /// longer valid" case) or an unreachable service.</summary>
    public Task<MeetingActionPreview?> GetMeetingActionAsync(
        string token, CancellationToken cancellationToken = default) =>
        GetAsync<MeetingActionPreview>(
            $"meeting-actions/{Uri.EscapeDataString(token)}", cancellationToken);

    /// <summary>D-717 — confirm a speaker action link
    /// (<c>POST /api/v1/app/meeting-actions/{token}</c>): consumes the token and
    /// applies the decision. Returns <c>null</c> when the token is no longer
    /// usable.</summary>
    public Task<MeetingActionOutcome?> ConfirmMeetingActionAsync(
        string token, CancellationToken cancellationToken = default) =>
        PostAsync<MeetingActionOutcome>(
            $"meeting-actions/{Uri.EscapeDataString(token)}", cancellationToken);

    private Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class =>
        ReadEnvelopeAsync<T>(ct => http.GetAsync(BasePath + path, ct), cancellationToken);

    private Task<T?> PostAsync<TBody, T>(
        string path, TBody body, CancellationToken cancellationToken)
        where T : class =>
        ReadEnvelopeAsync<T>(
            ct => http.PostAsJsonAsync(BasePath + path, body, JsonOptions, ct),
            cancellationToken);

    // No-body POST (e.g. a token confirm — the action is baked into the token).
    private Task<T?> PostAsync<T>(string path, CancellationToken cancellationToken)
        where T : class =>
        ReadEnvelopeAsync<T>(
            ct => http.PostAsync(BasePath + path, content: null, ct),
            cancellationToken);

    // Sends one request and unwraps the standard ApiResult envelope. The API
    // returns the envelope on both success and failure status codes, so the
    // body is read the same way either way; the payload is surfaced only when
    // the envelope reports success. An unreachable service, a timeout, or a
    // non-JSON body (such as a reverse-proxy error page) is surfaced as a null
    // payload — the page renders its error state and the caller never has to
    // catch. A genuine caller cancellation is allowed to propagate.
    private static async Task<T?> ReadEnvelopeAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await send(cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>(
                JsonOptions, cancellationToken);

            return result is { Success: true } ? result.Data : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
