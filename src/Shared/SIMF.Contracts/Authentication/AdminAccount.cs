namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/admin/users/reset-two-factor</c>. The actor
/// must hold the Administrator role; the target may not be the actor and
/// may not also hold the Administrator role (decision D-041).
/// </summary>
public sealed class AdminResetTwoFactorRequest
{
    /// <summary>The email address of the user whose 2FA is being reset.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// A free-text reason for the reset (10–500 chars) — audited and shown in
    /// the operation-log row. Examples: "user reported lost phone, called from
    /// known number 555-…", "user lost their recovery codes after a laptop
    /// re-image, identity verified in person 2026-05-23".
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
