namespace SIMF.ApiClient;

/// <summary>
/// Resolves and validates the SIMF API base address shared by the host
/// projects' typed-client registrations (SIMF.Web and SIMF.ControlPanel). The
/// value must be configured, and outside the Development environment it must be
/// HTTPS — the account/admin clients forward the caller's bearer token, so a
/// cleartext base address would leak it. Kept dependency-free (primitives in)
/// so the shared client library does not take a configuration/hosting
/// dependency; each host passes the configured value and its environment flag.
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
