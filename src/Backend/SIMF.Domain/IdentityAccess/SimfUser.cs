using Microsoft.AspNetCore.Identity;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A SIMF user account. Extends ASP.NET Core Identity's
/// <see cref="IdentityUser{TKey}"/>, which provides the email, password hash,
/// security stamp, lockout fields and two-factor state (SIMF-DAT-001 section 5.1
/// and Amendment B).
///
/// <para>D-106: QrId, RejectionReason and RejectionReasonArabic have moved
/// to <see cref="UserProfile"/> — they describe the visitor / participant's
/// onboarding state, not the account identity. Admin-typed users do not get
/// a QR (they don't attend with one) and do not get rejected; the
/// AdminAccountService approve/reject paths ensure a UserProfile row exists
/// before minting / writing the rejection text.</para>
/// </summary>
public class SimfUser : IdentityUser<Guid>
{
    /// <summary>The name shown in the user interface.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The account lifecycle state (SIMF-RPM-001 section 6).</summary>
    public AccountState AccountState { get; set; } = AccountState.Registered;

    /// <summary>
    /// The hardcoded user type (P7 — D-048; P8 — D-049). Determines
    /// where the user can sign in (CP for <see cref="UserType.Admin"/>;
    /// App / Website for <see cref="UserType.Visitor"/> and
    /// <see cref="UserType.Other"/>) and whether RBAC applies (Admin
    /// only). Defaults to <see cref="UserType.Visitor"/> — the
    /// least-privileged surface — so any row that loses its metadata
    /// falls into the safest bucket.
    ///
    /// <para>The user's profile-type (Visitor tier / Other partner-kind)
    /// no longer lives on this row — P8 moved it to
    /// <c>UserProfile.ProfileTypeId</c> so the lookup row sits with the
    /// rest of the per-user profile data.</para>
    /// </summary>
    public UserType UserType { get; set; } = UserType.Visitor;

    /// <summary>
    /// True when the account holds a temporary password and must change it
    /// before any other action (SIMF-FDS-001 Amendment A.5; enforced at
    /// sign-in by H4 — D-059). Default false — only explicitly-seeded
    /// accounts (the super-admin) or operator-forced credential rotations
    /// set this to true. A regular sign-up never sets the flag because
    /// the user's own password is the only one ever set on the row.
    /// </summary>
    public bool PasswordChangeRequired { get; set; }

    /// <summary>When the account was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the account was last updated (UTC); null if never.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// The most recent TOTP time-step accepted for this account. A code at or
    /// below this step is rejected as a replay (RFC 6238 section 5.2).
    /// </summary>
    public long? LastUsedTotpTimestep { get; set; }

    /// <summary>
    /// The relative path of the user's avatar file on disk, under the
    /// configured <c>Storage:AvatarBase</c>. For example <c>"abc123.png"</c>.
    /// Null when no avatar is set. Decision D-039 (2026-05-23) moved storage
    /// from <c>varbinary(max)</c> in the row to the filesystem, mirroring the
    /// IBS V10 car-image-upload convention.
    /// </summary>
    public string? AvatarRelativePath { get; set; }

    /// <summary>
    /// When the <see cref="AccountState"/> last changed (UTC) — P10 —
    /// D-051. Set on every transition through Approve / Reject / Disable
    /// / unblock, and back-filled to <see cref="CreatedAt"/> by the P10
    /// migration for existing rows. Lets admins answer "when was this
    /// rejected?" without joining the audit log.
    /// </summary>
    public DateTimeOffset? StateChangedAt { get; set; }

    /// <summary>
    /// The admin who last changed the <see cref="AccountState"/> (P10 —
    /// D-051). Nullable so legacy rows (and self-driven transitions like
    /// email verification) stay valid.
    /// </summary>
    public Guid? StateChangedByUserId { get; set; }
}
