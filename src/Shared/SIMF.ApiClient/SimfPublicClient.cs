using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Programme;

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
    private const string BasePath = "api/v1/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The public agenda — every published session
    /// (<c>GET /api/v1/programme/sessions</c>). Returns <c>null</c> on a
    /// failed envelope or an unreachable service.</summary>
    public Task<PublicSessions?> GetProgrammeSessionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicSessions>("programme/sessions", cancellationToken);

    /// <summary>The full public view of one session
    /// (<c>GET /api/v1/programme/sessions/{id}</c>). Returns <c>null</c> on a
    /// failed envelope (for example an unknown id) or an unreachable
    /// service.</summary>
    public Task<PublicSessionDetail?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<PublicSessionDetail>($"programme/sessions/{id}", cancellationToken);

    /// <summary>The public speakers list (<c>GET /api/v1/speakers</c>).
    /// Returns <c>null</c> on a failed envelope or an unreachable
    /// service.</summary>
    public Task<PublicSpeakers?> GetSpeakersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PublicSpeakers>("speakers", cancellationToken);

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BasePath + path);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);

            // The API returns the ApiResult envelope on both success and
            // failure HTTP status codes, so the body is read the same way
            // either way; the payload is surfaced only when the envelope
            // reports success.
            var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>(
                JsonOptions, cancellationToken);

            return result is { Success: true } ? result.Data : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A genuine caller cancellation — for example the Blazor circuit
            // ending — is not a service failure; let it propagate.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or NotSupportedException)
        {
            // An unreachable service, a timeout, or a non-JSON body (such as a
            // reverse-proxy error page) is surfaced as a null payload — the
            // page renders its error state and the caller never has to catch.
            return null;
        }
    }
}
