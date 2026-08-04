using SIMF.Common;

using SIMF.Common.Enums;

namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/admin/admins/reset-two-factor</c>. The actor
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

/// <summary>The response of <c>GET /api/v1/admin/admins/{id}/roles</c>
/// (Issue-1) — the RBAC role names an admin user currently holds, used to
/// pre-fill the user-roles editor.</summary>
public sealed record AdminUserRolesResponse(
    Guid UserId,
    IReadOnlyList<string> Roles);

/// <summary>The body of <c>PUT /api/v1/admin/admins/{id}/roles</c>
/// (Issue-1) — the complete set of role names the admin user should hold.
/// The server replaces the user's roles with exactly this set.</summary>
public sealed class AdminSetUserRolesRequest
{
    public List<string> Roles { get; set; } = new();
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
    /// The <c>ProfileTypes</c> row id that identifies the partner subtype.
    /// D-186: must reference an active row with
    /// <c>UserType = Visitor</c> AND <c>IsVisitor = false</c> (partner /
    /// staff scope) — the request is rejected if the chosen ProfileType
    /// is audience-side or admin-scope.
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

/// <summary>
/// The body of <c>PUT /api/v1/admin/visitors/{id}</c> (P1.3 / D-214 — promotes
/// the D-114 edit stub to a real edit). Updates an existing <b>Visitor</b>'s
/// editable fields: login <see cref="Email"/> (re-checked for uniqueness; a
/// change rolls the security stamp + revokes sessions), <see cref="DisplayName"/>,
/// and the optional <see cref="ProfileTypeId"/> tier. Approval state is NOT
/// editable here (use the approve/reject path). Administrator-only.
/// </summary>
public sealed class AdminUpdateVisitorRequest
{
    /// <summary>The visitor's login email; must not collide with another account.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The visitor tier (<c>ProfileTypes</c> row id). Optional; when
    /// supplied the row must be active with <c>UserType = Visitor</c> and
    /// <c>IsVisitor = true</c> (audience scope).</summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>Bi-Meeting rework — the admin-assigned per-user flag that lets this
    /// account request a speaker meeting (لقاء ثنائي). Independent of the VIP tier.</summary>
    public bool AllowsSpeakerMeeting { get; set; }

    /// <summary>Bi-Meeting rework — the admin-assigned per-user flag that lets this
    /// account request a delegation (وفد) meeting. Independent of the delegate flag.</summary>
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>B22 — the ISO alpha-2 nationality code (the same wire shape the
    /// self-service profile upsert and <see cref="AdminUserProfileView.NationalityCode"/>
    /// use). Optional: null / empty leaves the stored nationality untouched, so every
    /// existing caller keeps working. When supplied it must match an ACTIVE
    /// <c>Countries</c> row — the same rule the self-service path enforces. This is the
    /// only admin path that can correct a wrong nationality, and nationality gates
    /// delegation-meeting confirm eligibility.</summary>
    public string? NationalityCode { get; set; }

    /// <summary>FR-PHN-002 — an optional Saudi-mobile correction
    /// (<c>05XXXXXXXX</c> / <c>+9665XXXXXXXX</c>). Optional: null / empty leaves
    /// the stored number untouched, so every existing caller keeps working. When
    /// supplied it must pass the SAME shape rule as the self-service upsert and
    /// the walk-in desk, and it is stored canonicalised (DEF-PHN-003). Until this
    /// existed, every admin surface showed the mobile read-only and only the
    /// walk-in CREATE desk could type one — a wrong number could never be
    /// corrected.</summary>
    public string? SaudiMobile { get; set; }

    /// <summary>FR-PHN-002 — an optional international-mobile (E.164) correction.
    /// Same optional-means-unchanged semantics as <see cref="SaudiMobile"/>.</summary>
    public string? InternationalMobile { get; set; }
}

/// <summary>
/// The body of <c>PUT /api/v1/admin/others/{id}</c> (P1.3 / D-214). Updates an
/// existing partner-side (<b>Other</b>) account. Same shape as
/// <see cref="AdminUpdateVisitorRequest"/> but the partner subtype is
/// mandatory and must be partner-scope (<c>IsVisitor = false</c>).
/// Administrator-only.
/// </summary>
public sealed class AdminUpdateOtherRequest
{
    /// <summary>The account's login email; must not collide with another account.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The display name shown in the UI (2–128 characters).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The partner subtype (<c>ProfileTypes</c> row id). Required; the
    /// row must be active with <c>UserType = Visitor</c> and
    /// <c>IsVisitor = false</c> (partner scope).</summary>
    public Guid ProfileTypeId { get; set; }

    /// <summary>Bi-Meeting rework — the admin-assigned per-user speaker-meeting flag
    /// (see <see cref="AdminUpdateVisitorRequest.AllowsSpeakerMeeting"/>).</summary>
    public bool AllowsSpeakerMeeting { get; set; }

    /// <summary>Bi-Meeting rework — the admin-assigned per-user delegation-meeting flag
    /// (see <see cref="AdminUpdateVisitorRequest.AllowsDelegationMeeting"/>).</summary>
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>B22 — the ISO alpha-2 nationality code
    /// (see <see cref="AdminUpdateVisitorRequest.NationalityCode"/>). Optional.</summary>
    public string? NationalityCode { get; set; }

    /// <summary>FR-PHN-002 — an optional Saudi-mobile correction
    /// (see <see cref="AdminUpdateVisitorRequest.SaudiMobile"/>). Optional.</summary>
    public string? SaudiMobile { get; set; }

    /// <summary>FR-PHN-002 — an optional international-mobile correction
    /// (see <see cref="AdminUpdateVisitorRequest.InternationalMobile"/>). Optional.</summary>
    public string? InternationalMobile { get; set; }
}

/// <summary>
/// D-728 (owner item 9) — the body of
/// <c>POST /api/v1/admin/accounts/{id}/change-type</c>. Flips an existing
/// account between the audience (Visitor) and partner (Other) scope by
/// reassigning its profile type. <see cref="NewProfileTypeId"/> must be an
/// active profile type in the <b>opposite</b> scope to the account's current
/// one (a same-scope change is an edit, not a type change). Administrator-only.
/// </summary>
public sealed class AdminChangeAccountTypeRequest
{
    /// <summary>The target profile type (<c>ProfileTypes</c> row id). Required;
    /// must be active and its <c>IsVisitor</c> must be the opposite of the
    /// account's current scope.</summary>
    public Guid NewProfileTypeId { get; set; }
}

/// <summary>The body of a successful admin-created account (D-042).</summary>
public sealed record AdminCreateUserResponse(
    Guid UserId,
    string Email,
    int InviteExpiresInSeconds);

/// <summary>One row in the admin user-list view (D-042, D-044). <c>HasAvatar</c>
/// lets the grid render the account's profile-photo thumbnail (streamed from
/// <c>GET /admin/{visitors,others,admins}/{id}/avatar</c>) or an initials
/// fallback.</summary>
public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    string AccountState,
    bool TwoFactorEnabled,
    bool IsAdministrator,
    DateTime CreatedAt,
    // Whether the account has a profile photo (avatar) — the StoredFile presence
    // sentinel SimfUser.AvatarRelativePath (D-568). Trailing-optional (append-only,
    // wire-safe); defaults false for contexts that don't resolve it (bulk export,
    // the optimistic post-save row — both reload from the server anyway).
    bool HasAvatar = false);

/// <summary>The body of <c>POST /api/v1/admin/admins/bulk-delete</c>
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

/// <summary>D-164 (PDF §2.7.1, gap doc G2) — body of
/// <c>POST /api/v1/admin/visitors/bulk-approve</c> and
/// <c>POST /api/v1/admin/others/bulk-approve</c>. The security team's
/// "Select All" affordance: approve every selected pending user in one
/// request. Per-subject failures are reported in
/// <see cref="AdminBulkApprovalResponse"/> and do not block the rest.</summary>
public sealed class AdminBulkApprovalRequest
{
    /// <summary>The user ids to approve. Empty arrays are rejected with
    /// HTTP 400. The endpoint clamps to a max of 500 ids per request so
    /// the batch fits inside one SQL transaction window.</summary>
    public IList<Guid> Ids { get; set; } = new List<Guid>();
}

/// <summary>D-164 — outcome of a bulk approve. Per-subject failures
/// carry the user id, the email at the time of the attempt, and a
/// typed reason code so the CP can render an inline error list next to
/// the grid rows that did not flip.</summary>
public sealed record AdminBulkApprovalResponse(
    int Approved,
    int Skipped,
    IReadOnlyList<AdminBulkApprovalFailure> Failures);

/// <summary>D-164 — one failed-subject row in
/// <see cref="AdminBulkApprovalResponse.Failures"/>.</summary>
public sealed record AdminBulkApprovalFailure(
    Guid UserId,
    string? Email,
    string ReasonCode,
    string Message,
    string MessageArabic);

/// <summary>D-209 — the body of <c>POST /api/v1/admin/{visitors,others}/bulk-reject</c>.
/// Rejects a batch of pending users with one shared reason. The reason is
/// mandatory (10–500 chars — same rule as the single reject) and audited per
/// subject; empty id arrays / out-of-range reasons are rejected with HTTP 400
/// by the validator, and the worker clamps to a max of 500 ids per request.</summary>
public sealed class AdminBulkRejectRequest
{
    /// <summary>The pending user ids to reject.</summary>
    public IList<Guid> Ids { get; set; } = new List<Guid>();

    /// <summary>The shared rejection reason applied to every subject (10–500 chars).</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>D-209 — outcome of a bulk reject. Mirrors
/// <see cref="AdminBulkApprovalResponse"/> (reusing
/// <see cref="AdminBulkApprovalFailure"/> for the per-subject failure rows);
/// <see cref="Rejected"/> is the count that flipped to Rejected.</summary>
public sealed record AdminBulkRejectResponse(
    int Rejected,
    int Skipped,
    IReadOnlyList<AdminBulkApprovalFailure> Failures);

/// <summary>The body of <c>POST /api/v1/admin/admins/duplicate</c> (D-044 b).
/// Creates a new user as a copy of the source — same display-name pattern,
/// same Administrator-role membership, no password, fresh invite email.</summary>
public sealed class AdminDuplicateUserRequest
{
    /// <summary>The user id to copy.</summary>
    public Guid SourceId { get; set; }

    /// <summary>The email address for the new user.</summary>
    public string NewEmail { get; set; } = string.Empty;
}

/// <summary>The body of <c>POST /api/v1/admin/admins/export</c> (D-044 b).
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
/// approver only needs to see the identity to decide. <c>HasAvatar</c> drives the
/// grid profile-photo thumbnail (the D-568 <c>AvatarRelativePath</c> presence
/// sentinel).
/// </summary>
public sealed record AdminPendingUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    DateTime CreatedAt,
    // Trailing-optional (append-only, wire-safe); defaults false where unresolved.
    bool HasAvatar = false);

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
    // D-161 — the mobile-app authority any user assigned to this type
    // carries into the Flutter app. Serialised as the enum name
    // ("None" / "Staff" / "Moderator").
    string MobileAppRole,
    bool IsActive,
    // D-186 — audience-vs-partner split inside the Visitor scope.
    // true = audience profile type (VIP, Normal); false = partner /
    // staff profile type (Sponsor, Exhibitor, Media, Staff).
    bool IsVisitor,
    // D-725 — whether the type is offered in the app sign-up picker.
    // false = CP-only (admin-assigned), e.g. Staff / Moderator.
    bool IsAppRegisterable,
    // D-760 — whether accounts of this type appear in the "Meet People"
    // networking surfaces (partner directory + recommender). Trailing-optional
    // (append-only, wire-safe); defaults true. Only shown on the Others form.
    bool ShowInPartnerDirectory = true);

/// <summary>
/// D-115 — body of <c>POST /api/v1/admin/profile-types</c>. Creates a
/// new ProfileType row. D-186 collapsed UserType to Visitor-only for
/// non-admin profile types; the audience-vs-partner distinction is
/// expressed via <see cref="IsVisitor"/>. Per-UserType name uniqueness
/// is enforced server-side (case-insensitive).
/// </summary>
public sealed class AdminCreateProfileTypeRequest
{
    /// <summary>D-186: only "Visitor" is accepted for non-admin profile
    /// types (Admin-side profile types remain reserved for future use).
    /// The audience-vs-partner split lives on <see cref="IsVisitor"/>.</summary>
    public string UserType { get; set; } = string.Empty;

    /// <summary>English display name (1-128 chars; unique per UserType).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1-128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>App badge / picker colour — hex like "#FFD700" or a CSS variable (1-32 chars).</summary>
    public string PageColor { get; set; } = string.Empty;

    /// <summary>D-161 — the mobile-app authority any user assigned to this
    /// type carries into the Flutter app. Stringly-typed for forward
    /// compatibility ("None" / "Staff" / "Moderator"). Defaults to "None"
    /// when omitted. <c>UserType=Visitor</c> always resolves to the
    /// Visitor role at JWT issue time regardless of this value.</summary>
    public string? MobileAppRole { get; set; }

    /// <summary>Whether the row is visible in pickers from the moment of creation. Default true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>D-186 — audience (true) or partner / staff (false). Default
    /// true so a freshly created profile type lands on the Visitors
    /// approval queue until an admin explicitly flips it.</summary>
    public bool IsVisitor { get; set; } = true;

    /// <summary>D-725 (owner item 1) — whether the type appears in the app
    /// sign-up picker. Default true; set false for CP-only operational types
    /// (Staff, Moderator) that an admin assigns rather than a customer picks.</summary>
    public bool IsAppRegisterable { get; set; } = true;

    /// <summary>D-760 (owner request) — whether this type's accounts appear in
    /// the "Meet People (same interests)" networking surfaces. Default true.
    /// Meaningful only for partner (Other) types.</summary>
    public bool ShowInPartnerDirectory { get; set; } = true;
}

/// <summary>
/// D-115 — body of <c>PUT /api/v1/admin/profile-types/{id}</c>. Mutates
/// every field except <c>UserType</c> — a profile type cannot migrate
/// between Visitor and Admin scopes after creation. D-186: <see cref="IsVisitor"/>
/// can be flipped because it only re-routes the CP approval queue, not
/// the underlying user account.
/// </summary>
/// <remarks>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class (D-505, mirroring <c>UpdateHallRoute</c>) so it cannot drop a field
/// at bind time — D-843: <c>ShowInPartnerDirectory</c> was being forced back to its
/// <c>true</c> default on every edit, because the old inline bind model omitted it
/// and the drop therefore failed OPEN.</remarks>
public class AdminUpdateProfileTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string PageColor { get; set; } = string.Empty;
    /// <summary>D-161 — see <see cref="AdminCreateProfileTypeRequest.MobileAppRole"/>.</summary>
    public string? MobileAppRole { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>D-186 — audience (true) or partner / staff (false).</summary>
    public bool IsVisitor { get; set; } = true;

    /// <summary>D-725 (owner item 1) — whether the type appears in the app
    /// sign-up picker. Default true; false = CP-only (Staff, Moderator).</summary>
    public bool IsAppRegisterable { get; set; } = true;

    /// <summary>D-760 (owner request) — whether this type's accounts appear in
    /// the "Meet People (same interests)" networking surfaces. Default true.</summary>
    public bool ShowInPartnerDirectory { get; set; } = true;
}

/// <summary>
/// D-127 (amended D-425) — body of
/// <c>POST /api/v1/admin/{visitors,others}/register-onsite</c>. Walk-in / desk
/// registration shape — staff at the registration desk fills the full profile
/// face-to-face. D-425: the account is created <c>PendingApproval</c>; no QR is
/// minted at the desk — it is issued when an admin approves from the pending
/// queue. Email is optional (walk-ins frequently
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

    /// <summary>D-163 (PDF §2.6) — optional job title.</summary>
    public string? JobTitle { get; set; }

    /// <summary>2026-07-19 (owner) — Arabic job title (twin of <see cref="JobTitle"/>),
    /// so a VIP/delegate title is stored bilingually (used by the delegation head
    /// title). Optional; ≤100 chars.</summary>
    public string? JobTitleArabic { get; set; }

    /// <summary>V-1 (D-429) — the موج (Mawj) system identifier (المعرف في نظام موج).
    /// Optional everywhere; the dedicated VIP registration page captures it for
    /// VVIP/VIP visitors so the welcome-message export can key on it. ≤64 chars.</summary>
    public string? MawjId { get; set; }

    /// <summary>V-1 (D-429) — honorific / title (اللقب), e.g. "Minister". Optional;
    /// captured on the VIP page for the موج welcome message. ≤64 chars.</summary>
    public string? Honorific { get; set; }

    /// <summary>2026-07-19 (owner) — Arabic honorific (twin of <see cref="Honorific"/>),
    /// the fallback for a bilingual head-of-delegation title. Optional; ≤64 chars.</summary>
    public string? HonorificArabic { get; set; }

    /// <summary>V-1 (D-429) — preferred language for the موج welcome message
    /// (اللغة المفضلة), an IETF tag like "ar"/"en". Optional. ≤16 chars.</summary>
    public string? PreferredLanguage { get; set; }

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

    /// <summary>D-395 (الجنس) — the visitor's gender, captured at the desk.
    /// <see cref="Gender.Unspecified"/> when not picked. The column already
    /// exists on <c>UserProfile</c>; the walk-in form just didn't capture it.</summary>
    public Gender Gender { get; set; } = Gender.Unspecified;

    /// <summary>D-395 — optional vehicle plate number (Saudi plate shape, ≤7
    /// chars). The column already exists on <c>UserProfile</c> (D-371).</summary>
    public string? PlateNumber { get; set; }

    /// <summary>B3 — D-221 (الجهة): the picked <see cref="Organisation"/> id.
    /// Required at the walk-in desk (the validator rejects null / empty); the
    /// service rejects an unknown / inactive id with <c>OrganisationInvalid</c>.</summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>Picked interest ids (visitor-only; ignored for Other kind).</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    /// <summary>D-473 (#10) — true when the desk is registering a delegation (وفد)
    /// member. A delegate is an ordinary visitor with this flag set; the service
    /// then requires the nationality to be an invited country. Defaults false
    /// (a plain visitor walk-in).</summary>
    public bool IsDelegate { get; set; }
}

/// <summary>
/// D-127 (amended D-425) — response from the walk-in endpoint. Carries the data
/// the post-submit success modal needs: the chosen profile-type name + colour
/// and the user id (so a follow-up ID-document upload can reach the right row).
/// D-425: <see cref="QrId"/> is now EMPTY for a freshly created walk-in — the
/// account is PendingApproval and the QR is minted only on approval; the modal
/// treats an empty QrId as the "pending" state and shows no badge.
/// </summary>
public sealed record AdminWalkInRegistrationResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string QrId,
    string ProfileTypeName,
    string ProfileTypeNameArabic,
    string ProfileTypeColor);

/// <summary>D-473 (#10) — bulk-generate placeholder badges by profile type +
/// count (e.g. 10 VIP + 500 Normal), each Approved with a minted QR, optionally
/// flagged as delegation (وفد) members. The badges carry default data (no real
/// personal details) to be filled in / handed out later.</summary>
public sealed class AdminBulkGenerateBadgesRequest
{
    /// <summary>When true, every generated badge is flagged as a delegate.</summary>
    public bool IsDelegate { get; set; }

    /// <summary>The (profile type, count) batches to generate.</summary>
    public IList<BulkBadgeBatch> Batches { get; set; } = new List<BulkBadgeBatch>();

    /// <summary>D-751 (#10) — optional organiser recipient. When provided, the
    /// generated QR badge PNGs are zipped and emailed to this one address after
    /// generation. Null / empty leaves the badges DB-only (no email). Validated
    /// (trim, length, basic format) BEFORE any account is written, so a bad
    /// address is a clean 400 with nothing persisted.</summary>
    public string? RecipientEmail { get; set; }
}

/// <summary>D-473 (#10) — one bulk batch: how many badges of one profile type.</summary>
public sealed class BulkBadgeBatch
{
    public Guid ProfileTypeId { get; set; }
    public int Count { get; set; }
}

/// <summary>D-473 (#10) — the count of badges generated. D-751: <see
/// cref="EmailQueued"/> is true when an organiser recipient was supplied and the
/// ZIP of QR badge PNGs was enqueued for delivery (default false keeps existing
/// positional callers compiling).</summary>
public sealed record AdminBulkGenerateBadgesResponse(int Created, bool EmailQueued = false);

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
    string? JobTitle,
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
    // D-727 (owner item 5) — whether the subject has a profile photo (avatar) so
    // the CP view / pending-review can render it (streamed from
    // GET /admin/{visitors,others}/{id}/avatar). Mirrors HasIdImage.
    bool HasAvatar,
    IReadOnlyList<Guid> InterestIds,
    string? RejectionReason,
    string? RejectionReasonArabic,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Bi-Meeting rework — the two admin-assigned per-user meeting-eligibility flags.
    // Trailing-optional (append-only, wire-safe); default false where unresolved.
    bool AllowsSpeakerMeeting = false,
    bool AllowsDelegationMeeting = false);
