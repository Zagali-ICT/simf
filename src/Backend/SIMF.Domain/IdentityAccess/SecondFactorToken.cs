using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>Single-use ticket issued after the password step of sign-in and
/// exchanged at the second-factor step. Timestamps are Saudi local time.</summary>
public class SecondFactorToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public SecondFactorKind Kind { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public int AttemptCount { get; set; }
}
