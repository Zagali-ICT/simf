using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// What an <c>AccountCode</c> is for (SIMF-FDS-001 section 6,
/// SIMF-DAT-001 Amendment A.4).
/// </summary>
public enum AccountCodePurpose
{
    [Display(Description = nameof(ResAccountCodePurpose.EmailVerification), ResourceType = typeof(ResAccountCodePurpose))]
    EmailVerification = 0,

    [Display(Description = nameof(ResAccountCodePurpose.PasswordReset), ResourceType = typeof(ResAccountCodePurpose))]
    PasswordReset = 1,

    /// <summary>A one-time code emailed to a visitor as the sign-in second factor.</summary>
    [Display(Description = nameof(ResAccountCodePurpose.SignInOtp), ResourceType = typeof(ResAccountCodePurpose))]
    SignInOtp = 2,

    /// <summary>Part B — a one-time code emailed to verify the address a badge
    /// holder is activating their (passwordless, admin-created / walk-in) account
    /// against, before they set their first password. Additive value (D-110
    /// freeze permits appending new enum values); display falls back to the enum
    /// name (these codes are never enumerated in a UI).</summary>
    BadgeActivationOtp = 3,
}
