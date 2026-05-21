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
}
