namespace SIMF.ApiClient;

/// <summary>
/// Validates the shared SIMF API base address for both hosts' typed-client
/// registrations: it must be configured, and HTTPS outside Development — the
/// clients forward bearer tokens, so a cleartext base address would leak them.
/// Dependency-free (primitives in) so the client library needs no config/hosting
/// reference.
/// </summary>
public static class SimfApiBaseAddress
{
    /// <summary>
    /// Returns the validated base <see cref="Uri"/>. Throws
    /// <see cref="InvalidOperationException"/> when the value is missing, or when
    /// it is not HTTPS outside Development.
    /// </summary>
    public static Uri Resolve(string? configuredBaseUrl, bool isDevelopment)
    {
        var baseUrl = configuredBaseUrl
            ?? throw new InvalidOperationException(
                "Configuration value 'Api:BaseUrl' is required but was not found.");
        var baseUri = new Uri(baseUrl);
        if (!isDevelopment && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "'Api:BaseUrl' must use HTTPS outside the Development environment.");
        }
        return baseUri;
    }
}
