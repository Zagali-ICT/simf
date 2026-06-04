namespace SIMF.Application.IdentityAccess;

/// <summary>The outcome of a TOTP check — validity and the matched time-step.</summary>
public sealed record TotpResult(bool IsValid, long TimeStep);

/// <summary>Verifies an authenticator-app TOTP code against a user's secret.</summary>
public interface ITotpVerifier
{
    /// <summary>Checks <paramref name="code"/> against <paramref name="secret"/> now.</summary>
    TotpResult Verify(string secret, string code);
}
