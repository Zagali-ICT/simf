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

/// <summary>
/// The body of <c>POST /api/v1/admin/users</c>. The actor must hold the
/// Administrator role; the user is created in the <c>Approved</c> state
/// with no password — they receive an invitation email carrying a one-time
/// password-set code (decision D-042).
/// </summary>
public sealed class AdminCreateUserRequest
{
    /// <summary>The new user's email address; must not already be registered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When true, the new user is added to the Administrator role. Defaults to
    /// false. The wider role catalogue waits for gate D1 / CPD-001 OI-3.
    /// </summary>
    public bool GrantAdministratorRole { get; set; }
}

/// <summary>The body of a successful admin-created account (D-042).</summary>
public sealed record AdminCreateUserResponse(
    Guid UserId,
    string Email,
    int InviteExpiresInSeconds);

/// <summary>One row in the admin user-list view (D-042).</summary>
public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    string AccountState,
    bool TwoFactorEnabled,
    bool IsAdministrator,
    DateTimeOffset CreatedAt);

/// <summary>The body of <c>GET /api/v1/admin/users</c>.</summary>
public sealed record AdminUserListResponse(IReadOnlyList<AdminUserSummary> Users);
