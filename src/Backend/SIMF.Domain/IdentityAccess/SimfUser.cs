using Microsoft.AspNetCore.Identity;
using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A SIMF user account. Extends ASP.NET Core Identity's
/// <see cref="IdentityUser{TKey}"/>, where the email, password hash, security
/// stamp, lockout counters and two-factor state live.
///
/// <para>This row is the account identity and nothing more. The same person's
/// attendee side, the badge QR id and the rejection text among it, sits on
/// <c>UserProfile</c> in the App database, so there is no navigation from here
/// to it; the approve and reject paths create that row when it is missing.</para>
/// </summary>
public class SimfUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Registered, the default, is the state before the email is
    /// verified. Sign-in refuses it and Disabled outright, but PendingApproval
    /// and Rejected do sign in and are routed to their own screens by the
    /// account_state claim, so this is not on its own an access decision.</summary>
    public AccountState AccountState { get; set; } = AccountState.Registered;

    /// <summary>Decides the surface the account may sign in on: Admin the
    /// Control Panel, Visitor the App and Website, and the wrong surface is
    /// refused even with the right password. RBAC roles are honoured on Admin
    /// rows only; a visitor is tiered by <c>UserProfile.ProfileTypeId</c>
    /// instead. Defaults to Visitor, the least-privileged surface, so a row
    /// that loses this value falls into the safest bucket.</summary>
    public UserType UserType { get; set; } = UserType.Visitor;

    /// <summary>Blocks every path that mints a session until the password is
    /// replaced. Set by the seeder for the super-admin, by an operator forcing
    /// a rotation, and by sign-in itself once the password passes the
    /// configured maximum age. An ordinary sign-up never sets it: the only
    /// password ever put on the row is the user's own.</summary>
    public bool PasswordChangeRequired { get; set; }

    // Saudi local time, as is every other timestamp on this row.
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>The most recent TOTP time-step accepted for this account. A
    /// code at or below this step is rejected as a replay (RFC 6238 section
    /// 5.2).</summary>
    public long? LastUsedTotpTimestep { get; set; }

    /// <summary>The avatar's <c>StoredFile</c> row in the App database, as a
    /// bare Guid and deliberately not a foreign key: SQL Server has no
    /// cross-database FK syntax and D-157 forbids the reference anyway, so the
    /// service layer owns the link exactly as it does for
    /// <c>UserProfile.UserId</c>. Null means no avatar.
    ///
    /// <para>This was <c>AvatarRelativePath</c>, a string, until the column was
    /// found still advertising a path it had not held since the file store was
    /// unified. Nothing reads it to fetch bytes: the avatar endpoint resolves
    /// them by owner instead (<c>Service == Avatar &amp;&amp; OwnerEntityId ==
    /// userId</c>). It is a presence sentinel, the pointer the delete path
    /// retires, and the cache-buster input.</para></summary>
    public Guid? AvatarFileId { get; set; }

    /// <summary>When <see cref="AccountState"/> last changed. Written on every
    /// transition through approve, reject, disable and unblock, and back-filled
    /// to <see cref="CreatedAt"/> for rows that predate the column, so an admin
    /// can answer "when was this rejected?" without joining the audit log.</summary>
    public DateTime? StateChangedAt { get; set; }

    /// <summary>The admin behind that change. Null when there is no actor: a
    /// self-driven transition such as email verification, or a system sweep
    /// such as the dormant-account disable.</summary>
    public Guid? StateChangedByUserId { get; set; }

    /// <summary>When the password was last set. Null on an account whose
    /// password has not been changed since this column shipped, and the
    /// maximum-password-age check then measures from <see cref="CreatedAt"/>
    /// instead.</summary>
    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>The last completed sign-in, stamped when tokens are issued. Two
    /// things read it: the "previous sign-in" notice returned to the client,
    /// and the dormant-account sweep, which takes it as the activity signal and
    /// falls back to <see cref="CreatedAt"/> when it is null. That fallback
    /// would disable every long-standing account on the first run, which is why
    /// <c>DormantAccountService</c> ships switched off.</summary>
    public DateTime? LastSuccessfulSignInAt { get; set; }
}
