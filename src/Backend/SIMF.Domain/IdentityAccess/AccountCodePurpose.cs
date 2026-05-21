namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// What an <see cref="AccountCode"/> is for (SIMF-FDS-001 section 6,
/// SIMF-DAT-001 Amendment A.4).
/// </summary>
public enum AccountCodePurpose
{
    EmailVerification = 0,
    PasswordReset = 1,

    /// <summary>A one-time code emailed to a visitor as the sign-in second factor.</summary>
    SignInOtp = 2,
}
