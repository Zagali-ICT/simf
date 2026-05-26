using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>The kind of second factor a sign-in is completed with.</summary>
public enum SecondFactorKind
{
    /// <summary>An authenticator-app TOTP code — for Control Panel users.</summary>
    [Display(Description = nameof(ResSecondFactorKind.Totp), ResourceType = typeof(ResSecondFactorKind))]
    Totp = 0,

    /// <summary>A code emailed to the user — for visitors.</summary>
    [Display(Description = nameof(ResSecondFactorKind.EmailOtp), ResourceType = typeof(ResSecondFactorKind))]
    EmailOtp = 1,
}
