using SIMF.Common;

using SIMF.Common.Enums;

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
/// The body of <c>POST /api/v1/admin/admins</c> (P7c — renamed from
/// <c>/admin/staff</c>). Creates a new Control Panel <b>Admin</b> user
/// — the only <see cref="SIMF.Domain.IdentityAccess.UserType"/> that
/// carries RBAC roles per the P7 model (decision D-048). The new
/// account lands in <c>PendingApproval</c> with no password; the user
/// receives a 7-day invitation code (D-042). Approval is
/// Administrator-only and mints the QR id (D-046a + P4).
/// </summary>
public sealed class AdminCreateAdminRequest
{
    /// <summary>The new admin's email address; must not already be registered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The RBAC roles to grant the new admin. Today the only allowed
    /// value is <c>"Administrator"</c>; future fine-grained Admin-side
    /// roles plug in here. An empty list means "no role today" — the
    /// admin can sign in to the CP (UserType = Admin) but every action
    /// is gated by RBAC, so they will have very limited access.
    /// </summary>
    public IList<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// The body of <c>POST /api/v1/admin/others</c> (P7c — new). Creates a
/// new <b>Other</b> user — an event team / partner who signs in to the
/// Flutter app, not the CP. The <see cref="ProfileTypeId"/> picks the
/// Other subtype (Staff / Exhibitor / Sponsor / …) from the
/// <c>ProfileTypes</c> lookup. Administrator-only.
/// </summary>
public sealed class AdminCreateOtherRequest
{
    /// <summary>The new user's email address; must not already be registered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The <c>ProfileTypes</c> row id that identifies the subtype. Must
    /// reference an active row with <c>UserType = Other</c>.
    /// </summary>
    public Guid ProfileTypeId { get; set; }
}

/// <summary>
/// The body of <c>POST /api/v1/admin/visitors</c> (P3; P7c added
/// optional <see cref="ProfileTypeId"/>). Creates a new
/// <b>Visitor</b> user — an event attendee who signs in to the Flutter
/// app / Website. The Subtype (VVIP / VIP / Gold / …) is **optional**
/// at create time because a self-registered visitor has none until
/// approval. Administrator-only.
/// </summary>
public sealed class AdminCreateVisitorRequest
{
    /// <summary>The new visitor's email address; must not already be registered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The <c>ProfileTypes</c> row id that identifies the visitor tier
    /// (VVIP / VIP / Gold / …). Optional — null means "no tier today";
    /// the admin can set the tier later from the visitor's profile.
    /// When supplied, the row must be active with
    /// <c>UserType = Visitor</c>.
    /// </summary>
    public Guid? ProfileTypeId { get; set; }
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

/// <summary>
/// The body of an approval (P4). Carries no payload today — the user id is
/// in the route, the actor is the bearer token. A class (not a record) so
/// the FastEndpoints binder accepts the empty JSON body.
/// </summary>
public sealed class AdminApproveRequest
{
}

/// <summary>
/// The body of a rejection (P4). The reason is mandatory and audited so a
/// SOC reviewer can see why an account was refused.
/// </summary>
public sealed class AdminRejectRequest
{
    /// <summary>
    /// A free-text reason for the rejection (10–500 chars). Audited and
    /// shown in the operation-log row. Same shape as
    /// <see cref="AdminResetTwoFactorRequest.Reason"/>.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// One row in the pending-approval list (P4). A trimmed shape — the
/// approver only needs to see the identity to decide.
/// </summary>
public sealed record AdminPendingUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt);

/// <summary>
/// One row in the <c>ProfileTypes</c> lookup (P7c). Used by the CP
/// create / list pages to populate the subtype picker.
/// </summary>
public sealed record AdminProfileTypeSummary(
    Guid Id,
    string Name,
    string NameArabic,
    string PageColor,
    string UserType,
    bool IsActive);

/// <summary>
/// D-115 — body of <c>POST /api/v1/admin/profile-types</c>. Creates a
/// new ProfileType row scoped to a single <see cref="UserType"/>
/// (Visitor or Other today; Admin is rejected since admins don't carry
/// a profile type). Per-UserType name uniqueness is enforced
/// server-side (case-insensitive).
/// </summary>
public sealed class AdminCreateProfileTypeRequest
{
    /// <summary>"Visitor" or "Other". Any other value returns 400.</summary>
    public string UserType { get; set; } = string.Empty;

    /// <summary>English display name (1-128 chars; unique per UserType).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1-128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>App badge / picker colour — hex like "#FFD700" or a CSS variable (1-32 chars).</summary>
    public string PageColor { get; set; } = string.Empty;

    /// <summary>Whether the row is visible in pickers from the moment of creation. Default true.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// D-115 — body of <c>PUT /api/v1/admin/profile-types/{id}</c>. Mutates
/// every field except <c>UserType</c> — a profile type cannot migrate
/// between Visitor and Other after creation (decisions log).
/// </summary>
public sealed class AdminUpdateProfileTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string PageColor { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// D-127 — body of <c>POST /api/v1/admin/{visitors,others}/register-onsite</c>.
/// Walk-in / desk registration shape — staff at the registration desk fills
/// the full profile face-to-face and the user is auto-approved + a QR id
/// is minted in the same transaction. Email is optional (walk-ins frequently
/// don't have one; the QR badge is the access token); when missing the API
/// synthesizes <c>walkin-{guid}@simf.local</c> so Identity stays valid.
/// </summary>
public sealed class AdminWalkInRegistrationRequest
{
    /// <summary>Optional email; when blank the API synthesizes a placeholder.</summary>
    public string? Email { get; set; }

    /// <summary>The on-badge display name (2-128 chars).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Full name in Arabic (matches the registration form).</summary>
    public string ArabicName { get; set; } = string.Empty;

    /// <summary>Full name in English — exactly as on the passport / ID.</summary>
    public string EnglishName { get; set; } = string.Empty;

    /// <summary>Visitor tier / Other subtype — drives the badge colour.</summary>
    public Guid ProfileTypeId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 nationality code.</summary>
    public string NationalityCode { get; set; } = string.Empty;

    /// <summary>Optional date of birth.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Optional place of birth (≤128 chars).</summary>
    public string? PlaceOfBirth { get; set; }

    /// <summary>True when the visitor holds Saudi nationality — drives which
    /// ID number is required.</summary>
    public bool IsSaudi { get; set; }

    /// <summary>Saudi national id (10 digits) — required when <see cref="IsSaudi"/>.</summary>
    public string? NationalId { get; set; }

    /// <summary>Iqama number (10 digits) — required for non-Saudi residents.</summary>
    public string? IqamaNumber { get; set; }

    /// <summary>Passport number — required for non-Saudi visitors.</summary>
    public string? PassportNumber { get; set; }

    /// <summary>+966-prefixed Saudi mobile.</summary>
    public string? SaudiMobile { get; set; }

    /// <summary>International mobile (<c>+CC-local</c>).</summary>
    public string? InternationalMobile { get; set; }

    /// <summary>Picked interest ids (visitor-only; ignored for Other kind).</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();
}

/// <summary>
/// D-127 — response from the walk-in endpoint. Carries the data the
/// post-submit success modal needs to render the badge: the QR id (already
/// minted because the user is auto-approved), the chosen profile-type
/// name + colour, and the user id so a follow-up ID-document upload can
/// reach the right row.
/// </summary>
public sealed record AdminWalkInRegistrationResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string QrId,
    string ProfileTypeName,
    string ProfileTypeNameArabic,
    string ProfileTypeColor);

/// <summary>
/// D-127 / D-126 — body returned by the broadened admin profile-read endpoints
/// (<c>GET /api/v1/admin/{visitors,others}/{id}/profile</c>). Q-G reversed:
/// any admin can read any visitor's or Other's profile, regardless of state.
/// Every read fires a row-audit row via the D-109 SaveChanges interceptor
/// on the underlying touch.
/// </summary>
public sealed record AdminUserProfileView(
    Guid Id,
    string Email,
    string DisplayName,
    string UserType,
    string AccountState,
    Guid? ProfileTypeId,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    string? ProfileTypeColor,
    string? QrId,
    string? ArabicName,
    string? EnglishName,
    string? NationalityCode,
    DateOnly? DateOfBirth,
    string? PlaceOfBirth,
    bool IsSaudi,
    string? NationalId,
    string? IqamaNumber,
    string? PassportNumber,
    string? SaudiMobile,
    string? InternationalMobile,
    bool HasIdImage,
    IReadOnlyList<Guid> InterestIds,
    string? RejectionReason,
    string? RejectionReasonArabic,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
