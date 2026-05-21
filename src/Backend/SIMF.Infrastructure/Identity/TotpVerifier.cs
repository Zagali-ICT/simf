using OtpNet;
using SIMF.Application.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Verifies an authenticator-app TOTP code (RFC 6238) against the user's
/// base32-encoded secret, allowing a one-step clock-skew window each way.
/// </summary>
internal sealed class TotpVerifier : ITotpVerifier
{
    public bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(secret);
        }
        catch (ArgumentException)
        {
            // A malformed stored secret cannot verify anything — fail closed.
            return false;
        }

        return new Totp(secretBytes)
            .VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
