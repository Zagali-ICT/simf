using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// The short-lived, single-use ticket issued after the password step of sign-in
/// and exchanged at the second-factor step — the <c>mfaToken</c> / <c>otpToken</c>
/// of SIMF-API-001 section 12.4. The token value itself is never stored; only
/// its hash is (SIMF-FDS-001 Amendment A.3).
/// </summary>
public class SecondFactorToken
{
    public Guid Id { get; set; }

    /// <summary>The user the ticket was issued to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The hash of the ticket value; the value itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Which second factor this ticket expects.</summary>
    public SecondFactorKind Kind { get; set; }

    /// <summary>When the ticket was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the ticket expires (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the ticket was consumed (UTC); null while it is unused.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>How many incorrect second-factor codes have been submitted against it.</summary>
    public int AttemptCount { get; set; }
}
