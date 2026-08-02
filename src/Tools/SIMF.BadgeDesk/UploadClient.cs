using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// D-819 — posts a shift to <c>POST /api/v1/admin/offline/batch</c>.
///
/// <para>The desk holds NO credentials. The operator pastes a bearer token taken
/// from a Control Panel session at upload time, and it is kept in memory for
/// that upload only. A badge desk is an unattended machine on a folding table in
/// a public hall; storing an administrator's password on it would be the worst
/// credential exposure in the system, and the upload is a once-a-shift action
/// where pasting a token costs nothing.</para>
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
        var body = await response.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>(cancellationToken);

        if (!response.IsSuccessStatusCode || body?.Data is null)
        {
            // Surfaced verbatim rather than reduced to "upload failed": the two
            // answers an operator will actually see are a 403 because the
            // capability is not armed and a 401 because the pasted token has
            // expired, and those need different actions.
            var reason = body?.Error?.Message
                ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            throw new InvalidOperationException(reason);
        }

        return body.Data;
    }
}
