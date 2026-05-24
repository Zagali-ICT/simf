using SIMF.Common;

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
/// The body of <c>POST /api/v1/admin/staff</c> (renamed from
/// <c>/admin/users</c> in P3). The actor must hold the Administrator role;
/// a new Control Panel staff user is created in the <c>Approved</c> state
/// with no password — they receive an invitation email carrying a one-time
/// password-set code (decision D-042; P3 added the staff/visitor split).
/// </summary>
public sealed class AdminCreateUserRequest
{
    /// <summary>The new user's email address; must not already be registered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When true, the new user is added to the Administrator role. Defaults to
    /// false. The wider role catalogue (Staff / Scientific / Security) lands
    /// in P4.
    /// </summary>
    public bool GrantAdministratorRole { get; set; }
}

/// <summary>
/// The body of <c>POST /api/v1/admin/visitors</c> (added in P3). A team
/// member (Staff / Scientific / Security in P4; Administrator-as-fallback
/// today) creates a visitor account. Visitors carry no role; they sign in
/// to the Website / Flutter app and complete their own visitor profile
/// (D-046). The wider visitor-lifecycle approval workflow lands in P4.
/// </summary>
public sealed class AdminCreateVisitorRequest
{
    /// <summary>The new visitor's email address; must not already be registered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>The body of a successful admin-created account (D-042).</summary>
public sealed record AdminCreateUserResponse(
    Guid UserId,
    string Email,
    int InviteExpiresInSeconds);

/// <summary>One row in the admin user-list view (D-042, D-044).</summary>
public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    string AccountState,
    bool TwoFactorEnabled,
    bool IsAdministrator,
    DateTimeOffset CreatedAt);

/// <summary>The body of <c>POST /api/v1/admin/users/bulk-delete</c>
/// (decision D-044 b). One audit row is written per subject so SOC has
/// per-user visibility even on a batch action.</summary>
public sealed class AdminBulkDeleteRequest
{
    /// <summary>The user ids to delete. Empty arrays are rejected.</summary>
    public IList<Guid> Ids { get; set; } = new List<Guid>();

    /// <summary>A free-text reason (10-500 chars) shared across every audit row.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Result of a bulk-delete (D-044 b).</summary>
public sealed record AdminBulkDeleteResponse(int Deleted, int Skipped);

/// <summary>The body of <c>POST /api/v1/admin/users/duplicate</c> (D-044 b).
/// Creates a new user as a copy of the source — same display-name pattern,
/// same Administrator-role membership, no password, fresh invite email.</summary>
public sealed class AdminDuplicateUserRequest
{
    /// <summary>The user id to copy.</summary>
    public Guid SourceId { get; set; }

    /// <summary>The email address for the new user.</summary>
    public string NewEmail { get; set; } = string.Empty;
}

/// <summary>The body of <c>POST /api/v1/admin/users/export</c> (D-044 b).
/// When <see cref="Ids"/> is empty, the endpoint exports every user that
/// matches the (optional) <see cref="Query"/>.</summary>
public sealed class AdminExportUsersRequest
{
    /// <summary>The user ids to export. Empty means "all matching the query".</summary>
    public IList<Guid> Ids { get; set; } = new List<Guid>();

    /// <summary>The grid query whose result set to export (used only when <see cref="Ids"/> is empty).</summary>
    public GridQuery? Query { get; set; }
}

/// <summary>Result of a bulk import — per-row outcome summary (D-044 b).</summary>
public sealed record AdminImportUsersResponse(
    int Created,
    int Skipped,
    IReadOnlyList<AdminImportError> Errors);

/// <summary>One failed row in an import (D-044 b).</summary>
public sealed record AdminImportError(int Row, string Email, string Reason);
