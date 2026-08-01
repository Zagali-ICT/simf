namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A7-20 (NCA Secure Application-Development Standard) — one previously-used
/// password hash for an account, so a change / reset can refuse to re-use a
/// recent password. Append-only; pruned to the configured history depth. The
/// hash is the ASP.NET Identity password hash (one-way, per-account salt) — never
/// a reversible value.
/// </summary>
public class PasswordHistoryEntry
{
    public Guid Id { get; set; }

    /// <summary>The owning account (FK to <c>AspNetUsers.Id</c>).</summary>
    public Guid UserId { get; set; }

    /// <summary>The retired ASP.NET Identity password hash.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>When the password was retired (Saudi local).</summary>
    public DateTime CreatedAtUtc { get; set; }
}
