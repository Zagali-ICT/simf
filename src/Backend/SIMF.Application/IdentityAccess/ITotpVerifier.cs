namespace SIMF.Application.IdentityAccess;

/// <summary>Verifies an authenticator-app TOTP code against a user's secret.</summary>
public interface ITotpVerifier
{
    /// <summary>True when <paramref name="code"/> is valid for <paramref name="secret"/> now.</summary>
    bool Verify(string secret, string code);
}
