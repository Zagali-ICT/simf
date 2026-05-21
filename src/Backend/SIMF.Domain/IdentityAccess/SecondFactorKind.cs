namespace SIMF.Domain.IdentityAccess;

/// <summary>The kind of second factor a sign-in is completed with.</summary>
public enum SecondFactorKind
{
    /// <summary>An authenticator-app TOTP code — for Control Panel users.</summary>
    Totp = 0,

    /// <summary>A code emailed to the user — for visitors.</summary>
    EmailOtp = 1,
}
