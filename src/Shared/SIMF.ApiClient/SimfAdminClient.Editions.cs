// Part of SimfAdminClient — see SimfAdminClient.cs for the transport core.
// The yearly edition lifecycle: read the open year, and move it on.
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ApiClient;

public sealed partial class SimfAdminClient
{
    /// <summary>The year currently open.</summary>
    public Task<ApiCallResult<AdminEventEditionResponse>> GetCurrentEditionAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminEventEditionResponse>(
            HttpMethod.Get, "editions/current", content: null,
            accessToken, cancellationToken);

    /// <summary>Closes the current year into history and opens the given one.
    ///
    /// <para>This CLEARS every attendee's badge. The response reports how many,
    /// which is the only evidence an operator has that the re-issue ran.</para>
    /// </summary>
    public Task<ApiCallResult<AdminOpenEditionResponse>> OpenEditionAsync(
        AdminOpenEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOpenEditionResponse>(
            HttpMethod.Post, "editions/open",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);
}
