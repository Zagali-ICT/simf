using OtpNet;
using SIMF.Infrastructure.Identity;
using Xunit;

namespace SIMF.Application.Tests.IdentityAccess;

/// <summary>
/// Tests for <see cref="TotpVerifier"/>, including the secret-normalisation
/// behaviour added for myComment #35 (the key with spaces silently failed).
/// </summary>
public sealed class TotpVerifierTests
{
    private const string SecretWithSpaces = "dbji csx7 c3mj s2qa sjcl rbcl kiqk ovr3";

    [Fact]
    public void A_key_with_spaces_verifies_a_code_generated_from_the_same_key()
    {
        var verifier = new TotpVerifier();
        var bytes = Base32Encoding.ToBytes(SecretWithSpaces.Replace(" ", "").ToUpperInvariant());
        var code = new Totp(bytes).ComputeTotp(DateTime.UtcNow);

        var result = verifier.Verify(SecretWithSpaces, code);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void A_lowercase_key_without_spaces_still_verifies()
    {
        var verifier = new TotpVerifier();
        var bytes = Base32Encoding.ToBytes("dbjicsx7c3mjs2qasjclrbclkiqkovr3".ToUpperInvariant());
        var code = new Totp(bytes).ComputeTotp(DateTime.UtcNow);

        var result = verifier.Verify("dbjicsx7c3mjs2qasjclrbclkiqkovr3", code);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void An_empty_secret_rejects_every_code()
    {
        var verifier = new TotpVerifier();

        Assert.False(verifier.Verify(string.Empty, "123456").IsValid);
    }

    [Fact]
    public void A_malformed_secret_rejects_every_code_without_throwing()
    {
        var verifier = new TotpVerifier();

        Assert.False(verifier.Verify("not-base32-at-all-!!!", "123456").IsValid);
    }

    [Fact]
    public void A_wrong_code_is_rejected()
    {
        var verifier = new TotpVerifier();

        Assert.False(verifier.Verify(SecretWithSpaces, "000000").IsValid);
    }

    [Fact]
    public void The_matched_time_step_is_reported_so_replay_detection_can_run()
    {
        var verifier = new TotpVerifier();
        var bytes = Base32Encoding.ToBytes(SecretWithSpaces.Replace(" ", "").ToUpperInvariant());
        var now = DateTime.UtcNow;
        var code = new Totp(bytes).ComputeTotp(now);

        var result = verifier.Verify(SecretWithSpaces, code);

        // The TOTP time-step is the Unix epoch second divided by the 30-second window.
        var expectedStep = new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds() / 30;
        Assert.True(result.IsValid);
        Assert.InRange(result.TimeStep, expectedStep - 1, expectedStep + 1);
    }
}
