using Microsoft.AspNetCore.Identity;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A SIMF user account. Extends ASP.NET Core Identity's
/// <see cref="IdentityUser{TKey}"/>, which provides the email, password hash,
/// security stamp, lockout fields and two-factor state (SIMF-DAT-001 section 5.1
/// and Amendment B).
/// </summary>
public class SimfUser : IdentityUser<Guid>
{
    /// <summary>The name shown in the user interface.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The account lifecycle state (SIMF-RPM-001 section 6).</summary>
    public AccountState AccountState { get; set; } = AccountState.Registered;

    /// <summary>
    /// True when the account holds a temporary password and must change it
    /// before any other action (SIMF-FDS-001 Amendment A.5).
    /// </summary>
    public bool PasswordChangeRequired { get; set; } = true;

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
    /// The user's avatar image bytes (myComment #11). Held in the row rather
    /// than the filesystem so the avatar is atomic with the account and there
    /// is no separate path/permission to manage. Null when not set.
    /// </summary>
    public byte[]? Avatar { get; set; }

    /// <summary>The avatar's MIME content type — e.g. <c>image/png</c>.</summary>
    public string? AvatarContentType { get; set; }
}

