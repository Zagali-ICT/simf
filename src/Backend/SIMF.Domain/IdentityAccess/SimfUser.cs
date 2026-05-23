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
    /// The relative path of the user's avatar file on disk, under the
    /// configured <c>Storage:AvatarBase</c>. For example <c>"abc123.png"</c>.
    /// Null when no avatar is set. Decision D-039 (2026-05-23) moved storage
    /// from <c>varbinary(max)</c> in the row to the filesystem, mirroring the
    /// IBS V10 car-image-upload convention.
    /// </summary>
    public string? AvatarRelativePath { get; set; }

    /// <summary>
    /// A short, unique, opaque event-entry identifier (decision D-046).
    /// Minted by <c>IQrIdMinter</c> the moment <see cref="AccountState"/>
    /// transitions to <see cref="AccountState.Approved"/>. Encoded in the
    /// visitor's QR code at event entry; staff scan it to check the
    /// visitor in. Null until the account is approved. Crockford base32
    /// alphabet (no 0/O/1/I/L/U), 12 chars (≈ 60 bits of entropy), unique
    /// across the system.
    /// </summary>
    public string? QrId { get; set; }
}

