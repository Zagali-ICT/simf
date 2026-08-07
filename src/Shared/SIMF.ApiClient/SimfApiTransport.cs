// Tests: SIMF.ApiClient.Tests/SimfApiTransportTests.cs (the handler carries the trust-all only when the validated flag allowed it)
namespace SIMF.ApiClient;

/// <summary>
/// The validated transport for the typed API clients: the base address and the
/// primary handler, resolved together from ONE reading of the two Api:* settings.
/// <para>
/// Both hosts used to read Api:AllowSelfSignedCertificate, build their own
/// handler from it, and separately hand a bool to the address validator. Those
/// are two paths from one setting, and nothing made them agree - a host could
/// install the certificate bypass while telling the validator it had not. Here
/// the flag is validated and applied by the same object, so the handler cannot
/// carry a bypass the validation did not see.
/// </para>
/// Dependency-free (primitives in, BCL out) so the client library still needs no
/// configuration or hosting reference.
/// </summary>
public sealed class SimfApiTransport
{
    private readonly bool allowSelfSignedCertificate;

    private SimfApiTransport(Uri baseAddress, bool allowSelfSignedCertificate)
    {
        BaseAddress = baseAddress;
        this.allowSelfSignedCertificate = allowSelfSignedCertificate;
    }

    /// <summary>The validated base address for every typed client.</summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Validates the pair and returns the transport. Throws
    /// <see cref="InvalidOperationException"/> on the same conditions as
    /// <see cref="SimfApiBaseAddress.Resolve"/>.
    /// </summary>
    public static SimfApiTransport Resolve(
        string? configuredBaseUrl, bool isDevelopment, bool allowSelfSignedCertificate)
    {
        var baseAddress = SimfApiBaseAddress.Resolve(
            configuredBaseUrl, isDevelopment, allowSelfSignedCertificate);

        return new SimfApiTransport(baseAddress, allowSelfSignedCertificate);
    }

    /// <summary>
    /// The primary handler for a typed client. Pass as a method group to
    /// ConfigurePrimaryHttpMessageHandler; HttpClientFactory calls it per handler
    /// rotation, so it returns a fresh instance each time.
    /// </summary>
    public HttpMessageHandler CreatePrimaryHandler()
    {
        var handler = new HttpClientHandler();
        if (allowSelfSignedCertificate)
        {
            // Reached only for a pairing Resolve accepted: loopback, or
            // Development. See the guard in SimfApiBaseAddress.Resolve.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return handler;
    }
}
