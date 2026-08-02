using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use, time-limited code sent to a user — for email verification or
/// password reset. Generalises the email-verification code through the
/// <see cref="Purpose"/> field (SIMF-FDS-001 section 6, SIMF-DAT-001
/// Amendment A.4).
/// </summary>
public class AccountCode
{
    public Guid Id { get; set; }

    /// <summary>The user the code was issued to.</summary>
    public Guid UserId { get; set; }

    /// <summary>What the code is for.</summary>
    public AccountCodePurpose Purpose { get; set; }

    /// <summary>The code value.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>When the code expires (Saudi local).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the code was created (Saudi local).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the code was consumed (Saudi local); null while it is unused.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>
    /// How many incorrect values have been submitted against this code. The
    /// code is invalidated once this passes the configured cap (SIMF-FDS-001
    /// Amendment A.1).
    /// </summary>
    public int AttemptCount { get; set; }
}
