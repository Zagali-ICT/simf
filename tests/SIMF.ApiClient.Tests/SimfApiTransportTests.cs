using System.Net.Security;

namespace SIMF.ApiClient.Tests;

/// <summary>Tests for the transport both hosts use to register their typed
/// clients. The point of the type is that the certificate override and the
/// validation come from one flag, so these assert on the HANDLER it produces
/// rather than on what the caller intended.</summary>
public sealed class SimfApiTransportTests
{
    [Fact]
    public void Resolve_exposes_the_validated_base_address()
    {
        var transport = SimfApiTransport.Resolve(
            "https://api.simf.test/", isDevelopment: false, allowSelfSignedCertificate: false);

        Assert.Equal(new Uri("https://api.simf.test/"), transport.BaseAddress);
    }

    [Fact]
    public void The_handler_does_not_override_validation_when_the_flag_is_off()
    {
        var transport = SimfApiTransport.Resolve(
            "https://api.simrsnf.com/", isDevelopment: false, allowSelfSignedCertificate: false);

        using var handler = Assert.IsType<HttpClientHandler>(transport.CreatePrimaryHandler());

        // Null => the platform's ordinary chain validation applies.
        Assert.Null(handler.ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public void The_handler_overrides_validation_only_for_a_pairing_resolve_accepted()
    {
        var transport = SimfApiTransport.Resolve(
            "https://localhost:12340/", isDevelopment: false, allowSelfSignedCertificate: true);

        using var handler = Assert.IsType<HttpClientHandler>(transport.CreatePrimaryHandler());
        var callback = handler.ServerCertificateCustomValidationCallback;

        // Asserted behaviourally rather than by delegate identity: the override
        // accepts a certificate whose name does not match the host, which is the
        // property that makes it dangerous off loopback.
        Assert.NotNull(callback);
        Assert.True(callback!(
            new HttpRequestMessage(), null, null, SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void A_rejected_pairing_never_yields_a_transport()
    {
        // The divergence this type exists to prevent: no object, so no handler,
        // so the bypass cannot be installed by a host that ignored the throw.
        Assert.Throws<InvalidOperationException>(
            () => SimfApiTransport.Resolve(
                "https://api.simrsnf.com/", isDevelopment: false, allowSelfSignedCertificate: true));
    }

    [Fact]
    public void Each_call_returns_a_fresh_handler()
    {
        // HttpClientFactory calls the factory once per handler rotation and
        // disposes what it gets, so a shared instance would be disposed twice.
        var transport = SimfApiTransport.Resolve(
            "https://api.simf.test/", isDevelopment: false, allowSelfSignedCertificate: false);

        using var first = transport.CreatePrimaryHandler();
        using var second = transport.CreatePrimaryHandler();

        Assert.NotSame(first, second);
    }
}
