using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use, time-limited code sent to a user — for email verification or
/// password reset. Generalises the email-verification code through the
/// <see cref="Purpose"/> field (SIMF-FDS-001 section 6, SIMF-DAT-001
/// Amendment A.4).
/// </summary>
public class AccountCode : BaseEntity
{
    /// <summary>The code value.</summary>
    public string Code { get; set; } = string.Empty;// must be hashed or encyrpted

    /// <summary>What the code is for.</summary>
    public AccountCodePurpose Purpose { get; set; }

    /// <summary>When the code expires (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// How many incorrect values have been submitted against this code. The
    /// code is invalidated once this passes the configured cap (SIMF-FDS-001
    /// Amendment A.1).
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>When the code was consumed (UTC); null while it is unused.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
