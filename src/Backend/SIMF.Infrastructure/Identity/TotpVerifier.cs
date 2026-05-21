using OtpNet;
using SIMF.Application.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Verifies an authenticator-app TOTP code (RFC 6238) against the user's
/// base32-encoded secret, allowing a one-step clock-skew window each way, and
/// reports the matched time-step so a replay can be detected.
/// </summary>
internal sealed class TotpVerifier : ITotpVerifier
{
    public TotpResult Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return new TotpResult(false, 0);
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(secret);
        }
        catch (ArgumentException)
        {
            // A malformed stored secret cannot verify anything — fail closed.
            return new TotpResult(false, 0);
        }

        var isValid = new Totp(secretBytes).VerifyTotp(
            code, out var matchedStep, new VerificationWindow(previous: 1, future: 1));
        return new TotpResult(isValid, matchedStep);
    }
}
