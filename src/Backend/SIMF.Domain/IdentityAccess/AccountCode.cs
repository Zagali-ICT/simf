using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>Single-use emailed code; <see cref="Purpose"/> selects email
/// verification or password reset. Timestamps are Saudi local time.</summary>
public class AccountCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AccountCodePurpose Purpose { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    /// <summary>Wrong submissions; the code is invalidated past the configured cap.</summary>
    public int AttemptCount { get; set; }
}
