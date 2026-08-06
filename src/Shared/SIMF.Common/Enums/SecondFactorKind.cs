using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// What kind of step a single-use sign-in ticket expects to complete the
/// sign-in: a second factor (<see cref="Totp"/> / <see cref="EmailOtp"/>) or,
/// for an account holding a forced-change credential, the password-change step
/// (<see cref="PasswordChange"/>).
/// </summary>
public enum SecondFactorKind
{
    /// <summary>An authenticator-app TOTP code — for Control Panel users.</summary>
    [Display(Description = nameof(ResSecondFactorKind.Totp), ResourceType = typeof(ResSecondFactorKind))]
    Totp = 0,

    /// <summary>A code emailed to the user — for visitors.</summary>
    [Display(Description = nameof(ResSecondFactorKind.EmailOtp), ResourceType = typeof(ResSecondFactorKind))]
    EmailOtp = 1,

    /// <summary>
    /// A forced-password-change ticket. Issued at the password step (in
    /// place of the old <c>AUTH_PASSWORD_CHANGE_REQUIRED</c> 403) when a Control
    /// Panel account holds a seeded/admin-rotated credential it must replace
    /// before any session is minted. Exchanged at
    /// <c>POST /auth/complete-password-change</c>. Internal plumbing only — never
    /// rendered, so no display resource is attached.
    /// </summary>
    PasswordChange = 2,

    /// <summary>
    /// #2 (Q1, 2026-07-30) — a mandatory authenticator-enrolment ticket. Issued at
    /// the password step, in place of an access token, when a Control Panel
    /// account signs in with no authenticator secret paired. Exchanged at
    /// <c>POST /auth/totp/enrolment/start</c> (returns the QR) and then
    /// <c>POST /auth/totp/enrolment/complete</c> (verifies the first code and
    /// issues the session). Internal plumbing only — never rendered, so no
    /// display resource is attached. The persisted column is
    /// <c>nvarchar(16)</c> (SecondFactorTokenConfiguration), so the NAME must
    /// stay within 16 characters.
    /// </summary>
    TotpEnrolment = 3,
}
