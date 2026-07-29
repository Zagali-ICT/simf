// The TOTP generator is the one piece of this suite that can fail SILENTLY and
// look like a product defect: a wrong code just gets rejected, and the sweep
// then reports "sign-in did not complete" for every route — 97 identical
// failures whose message blames the credentials. Pinning it to the published
// RFC 6238 vectors means a maths error is a one-line failure here instead.
//
// Vectors are RFC 6238 Appendix B, SHA-1, secret = ASCII "12345678901234567890"
// (base32 GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ). The RFC prints 8 digits; SIMF uses
// 6, which is the same truncation taken mod 10^6.
using Xunit;

namespace SIMF.E2E.Tests;

public sealed class TotpTests
{
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Matches_the_RFC_6238_vectors(long unixSeconds, string expected) =>
        Assert.Equal(
            expected,
            Totp.Now(RfcSecret, DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));

    [Fact]
    public void Accepts_the_padded_and_lower_case_forms_of_a_secret()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(1111111109L);
        var canonical = Totp.Now(RfcSecret, at);
        Assert.Equal(canonical, Totp.Now(RfcSecret.ToLowerInvariant(), at));
        Assert.Equal(canonical, Totp.Now(RfcSecret + "====", at));
    }

    [Fact]
    public void Produces_six_digits_including_leading_zeros()
    {
        var code = Totp.Now(RfcSecret, DateTimeOffset.FromUnixTimeSeconds(1234567890L));
        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.True(char.IsAsciiDigit(c)));
    }
}
