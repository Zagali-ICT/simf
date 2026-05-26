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
}
