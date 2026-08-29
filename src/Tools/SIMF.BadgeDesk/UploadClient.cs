using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// Posts a shift to <c>POST /api/v1/admin/offline/batch</c>.
///
/// <para>The desk holds no credential AT REST. The operator pastes a bearer
/// token taken from a Control Panel session and it is kept in memory for the
/// life of the process, so the background uploader can go on draining the
/// backlog without asking again; closing the app forgets it. A badge desk is an
/// unattended machine on a folding table in a public hall, so writing an
/// administrator's token to its disk would be the worst credential exposure in
/// the system. A copy in RAM costs the operator one paste per launch and leaves
/// nothing behind.</para>
/// </summary>
public sealed class UploadClient(HttpClient httpClient)
{
    /// <summary>Rows per request. The server caps a batch at 500; a desk with a
    /// larger backlog uploads in several passes, and each pass is independently
    /// idempotent, so an interrupted upload is simply resumed.</summary>
    public const int MaxBatchSize = 500;

    public async Task<OfflineBadgeBatchResponse> UploadAsync(
        string baseUrl,
        string bearerToken,
        string deskLabel,
        IList<OfflineBadgeRegistration> registrations,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "api/v1/admin/offline/batch"))
        {
            Content = JsonContent.Create(new OfflineBadgeBatchRequest
            {
                DeskLabel = deskLabel,
                Registrations = registrations,
            }),
        };
        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await httpClient.SendAsync(message, cancellationToken);

        // THE STATUS IS READ BEFORE THE BODY IS TOUCHED, and it alone decides
        // whether the pasted token is still good. Parsing first looked tidier and
        // was a crash waiting for a bad shift: the API answers 401 with no body
        // at all, and reading empty content as JSON throws — so the one failure
        // that most needed reporting, an expired credential, was the one that
        // took the desk down instead. It matters more now than it did: the
        // background loop meets this answer with nobody watching.
        if (!response.IsSuccessStatusCode)
        {
            throw new UploadFailedException(
                response.StatusCode,
                await DescribeFailureAsync(response, cancellationToken));
        }

        ApiResult<OfflineBadgeBatchResponse>? body;
        try
        {
            body = await response.Content
                .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A venue's captive portal answers 200 with its own login page, and
            // that is the network a badge desk actually sits on. Unhandled, it
            // escaped as a NotSupportedException past every catch in the app.
            // Reported as a failure it is just "not really on the network yet",
            // which is exactly what the retry loop is for.
            throw new UploadFailedException(
                response.StatusCode,
                "the reply was not a SIMF response — a captive portal or a proxy "
                + "answered instead of the API");
        }

        if (body?.Data is null)
        {
            throw new UploadFailedException(
                response.StatusCode,
                body?.Error?.Message ?? "the API returned an empty result");
        }

        return body.Data;
    }

    /// <summary>
    /// Why the server said no, in the words it used.
    ///
    /// <para>Surfaced verbatim rather than reduced to "upload failed": the two
    /// answers an operator will actually see are a 403 because the capability is
    /// not armed and a 401 because the pasted token has expired, and those need
    /// different actions. Falls back to the status line when there is no body to
    /// read — which is the normal shape of a 401 — because a report is still
    /// better than an exception nobody catches.</para>
    /// </summary>
    private static async Task<string> DescribeFailureAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content
                .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>(cancellationToken);
            if (body?.Error?.Message is { Length: > 0 } message) { return message; }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException
                                      or HttpRequestException)
        {
            // No body, or not JSON at all. The status line below is the report.
        }

        return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
    }
}

/// <summary>
/// The upload did not land, carrying the status that says why.
///
/// <para>Derives from <see cref="InvalidOperationException"/> because that is
/// what this client threw before it carried a status code, so every catch in the
/// desk still reads.</para>
///
/// <para><b>The status is not decoration.</b> The background uploader retries a
/// failure but DISCARDS the token on a credential answer, and without the code it
/// would have to match on message text. A stale token replayed on a timer for
/// three days is how an administrator's account gets locked out.</para>
/// </summary>
public sealed class UploadFailedException(HttpStatusCode statusCode, string message)
    : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>True when the server refused the CREDENTIAL rather than the
    /// batch: 401, the pasted token has expired, or 403, this account does not
    /// hold the offline-upload capability. Both need a person; neither is fixed
    /// by waiting.</summary>
    public bool IsCredentialFailure =>
        StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
