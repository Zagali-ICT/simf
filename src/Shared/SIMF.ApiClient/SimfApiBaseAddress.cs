// Tests: SIMF.ApiClient.Tests/SimfApiBaseAddressTests.cs (base-address validation + the trust-all guard)
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
    /// <see cref="InvalidOperationException"/> when the value is missing, when
    /// it is not HTTPS outside Development, or when
    /// <paramref name="allowSelfSignedCertificate"/> is paired with a
    /// non-loopback address outside Development.
    /// </summary>
    /// <param name="allowSelfSignedCertificate">
    /// The Api:AllowSelfSignedCertificate setting. No default on purpose: a
    /// caller that could omit it would install the bypass while skipping the
    /// check on it.
    /// </param>
    public static Uri Resolve(
        string? configuredBaseUrl, bool isDevelopment, bool allowSelfSignedCertificate)
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

        // Api:AllowSelfSignedCertificate installs
        // DangerousAcceptAnyServerCertificateValidator, which accepts ANY
        // certificate from ANY host, so TLS stops authenticating the peer and
        // only encrypts. On a loopback address that is tolerable: a loopback IP
        // has no name resolution and no network hop to get between the two
        // processes.
        // Against a public origin it means the admin password, the TOTP code and
        // the perm:* bearer token these clients carry are handed to whoever
        // answers for the name. Refusing to boot is deliberate: the alternative
        // is a server that looks healthy while authenticating nobody.
        if (!isDevelopment && allowSelfSignedCertificate && !baseUri.IsLoopback)
        {
            throw new InvalidOperationException(
                "'Api:AllowSelfSignedCertificate' is enabled for the non-loopback address " +
                $"'{baseUri}'. That disables certificate validation on a public origin, " +
                "exposing the forwarded bearer token to an on-path attacker. Set " +
                "'Api:AllowSelfSignedCertificate' to false, or point 'Api:BaseUrl' at loopback.");
        }

        return baseUri;
    }
}
