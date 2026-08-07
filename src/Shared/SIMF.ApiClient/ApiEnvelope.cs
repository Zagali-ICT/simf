using System.Text.Json;
using SIMF.Common;

namespace SIMF.ApiClient;

/// <summary>
/// Reads the standard <see cref="ApiResult{T}"/> envelope off a response.
///
/// <para>This exists because the same envelope read was written twice — once in
/// <c>SimfAdminClient</c>, once in <c>SimfAccountClient</c> — and carried the
/// same defect in both, so patching it twice would have left the next client to
/// copy it a third time. It is not a helper for tidiness; it is the one place
/// that knows how a SIMF response is decoded.</para>
///
/// <para><b>The defect.</b> An error envelope carries <c>"data": null</c>.
/// <c>ApiResult&lt;T&gt;.Data</c> is declared <c>T?</c>, but for an unconstrained
/// <c>T</c> that annotation is erased for value types — with <c>T = bool</c> the
/// property really is <c>bool</c>, and System.Text.Json throws rather than
/// binding null to it. Both clients caught <c>JsonException</c> in the same arm
/// as a dead socket, so a clean, specific refusal from the API reached the
/// operator as "The SIMF service could not be reached." Seventy methods across
/// the two clients return <c>ApiCallResult&lt;bool&gt;</c>, so every typed
/// refusal on any of them read as an outage.</para>
///
/// <para>Found on a live render, not by a test: the Control Panel reported the
/// service as unreachable while the API had answered <c>400
/// ADMIN_CANNOT_RESET_SELF</c> — an admin may not clear their own second factor,
/// which is correct and worth saying out loud.</para>
/// </summary>
internal static class ApiEnvelope
{
    /// <summary>
    /// Deserialises the envelope, recovering the API's own error when the
    /// payload cannot be bound to <typeparamref name="T"/>.
    ///
    /// <para>Returns <c>null</c> only for a literal <c>null</c> body, which the
    /// callers already report as an empty response. Anything genuinely
    /// unreadable — an HTML error page from a proxy, an empty body, a truncated
    /// stream — still throws, so it keeps reaching the transport-failure arm and
    /// "could not be reached" stays true whenever it is said.</para>
    /// </summary>
    public static async Task<ApiResult<T>?> ReadAsync<T>(
        HttpContent content,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        // Read once so the fallback can re-parse the SAME payload; the default
        // HttpClient completion option already buffers the response, so this is
        // not an extra round trip, whereas re-reading the stream would be.
        //
        // A string rather than the raw bytes on purpose: ReadAsStringAsync
        // honours the Content-Type charset exactly as the ReadFromJsonAsync it
        // replaces did, while JsonSerializer over a byte span would assume UTF-8
        // and silently mangle anything else. The API emits UTF-8 today — this
        // costs one allocation to avoid depending on that staying true.
        var payload = await content.ReadAsStringAsync(cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<ApiResult<T>>(payload, options);
        }
        catch (JsonException)
        {
            // The envelope may still be perfectly well-formed — it is the
            // PAYLOAD slot that cannot hold a T. Re-read with a data-agnostic
            // payload type and keep the error, which is the part the operator
            // actually needs.
            var loose = JsonSerializer.Deserialize<ApiResult<JsonElement?>>(payload, options);
            if (loose?.Error is null)
            {
                // Not an error envelope, so there is no message to rescue and no
                // honest T to return. Deliberately NOT defaulted to `false`/`0`:
                // on a bool endpoint that would report a failed operation as a
                // completed one that returned "no".
                throw;
            }

            return new ApiResult<T>
            {
                Success = false,
                Error = loose.Error,
                Meta = loose.Meta,
            };
        }
    }
}
