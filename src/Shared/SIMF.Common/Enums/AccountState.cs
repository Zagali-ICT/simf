using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// The lifecycle state of a user account (SIMF-RPM-001 section 6,
/// SIMF-DAT-001 section 5.1).
/// </summary>
public enum AccountState
{
    [Display(Description = nameof(ResAccountState.Registered), ResourceType = typeof(ResAccountState))]
    Registered = 0,

    [Display(Description = nameof(ResAccountState.EmailVerified), ResourceType = typeof(ResAccountState))]
    EmailVerified = 1,

    [Display(Description = nameof(ResAccountState.PendingApproval), ResourceType = typeof(ResAccountState))]
    PendingApproval = 2,

    [Display(Description = nameof(ResAccountState.Approved), ResourceType = typeof(ResAccountState))]
    Approved = 3,

    [Display(Description = nameof(ResAccountState.Rejected), ResourceType = typeof(ResAccountState))]
    Rejected = 4,

    [Display(Description = nameof(ResAccountState.Disabled), ResourceType = typeof(ResAccountState))]
    Disabled = 5,
}
