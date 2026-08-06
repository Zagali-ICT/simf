using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// What an <c>AccountCode</c> is for.
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

    /// <summary>A one-time code emailed to verify the address a badge
    /// holder is activating their (passwordless, admin-created / walk-in) account
    /// against, before they set their first password. Additive value (appending
    /// new enum values is allowed); display falls back to the enum
    /// name (these codes are never enumerated in a UI).</summary>
    BadgeActivationOtp = 3,

    /// <summary>A one-time code emailed to a signed-in user as the step-up
    /// confirmation before a biometric (Face-ID) device key is enrolled, so a
    /// borrowed-but-unlocked phone can't silently bind a new credential without
    /// also holding the account's email. Additive value (appending new enum
    /// values is allowed); display falls back to the enum name (these
    /// codes are never enumerated in a UI).</summary>
    BiometricEnrolStepUp = 4,

    /// <summary>A one-time code emailed to the NEW address a signed-in user
    /// (or a self-service change) is moving their login email to, so the change
    /// only completes once the user proves they control the new inbox. Additive
    /// value (appending new enum values is allowed); display falls back
    /// to the enum name (these codes are never enumerated in a UI).</summary>
    EmailChangeVerification = 5,
}
