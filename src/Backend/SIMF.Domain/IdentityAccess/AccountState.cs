namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// The lifecycle state of a user account (SIMF-RPM-001 section 6,
/// SIMF-DAT-001 section 5.1).
/// </summary>
public enum AccountState
{
    Registered = 0,
    EmailVerified = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Disabled = 5,
}
