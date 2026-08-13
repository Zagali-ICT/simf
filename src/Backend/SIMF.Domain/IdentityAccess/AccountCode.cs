// Tests: SIMF.Api.Tests/AuthFlow.cs (recovers the plaintext from CodeHash by
//        brute force, proving the column holds a hash) and
//        SIMF.Api.Tests/RetentionPurgeServiceTests.cs (expiry purge).
using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use, time-limited code sent to a user, for verifying an email
/// address or resetting a password. One table serves both, distinguished by
/// <see cref="Purpose"/>. The row holds only a keyed hash of the code
/// (<see cref="CodeHash"/>) — the code itself is emailed and never persisted.
/// </summary>
public class AccountCode
{
    public Guid Id { get; set; }

    /// <summary>The user the code was issued to.</summary>
    public Guid UserId { get; set; }

    public AccountCodePurpose Purpose { get; set; }

    /// <summary>The keyed HMAC of the code, never the code itself. Written only by
    /// <c>AccountCodeHasher.Hash</c>, and only ever compared against another hash of
    /// a submitted value — nothing queries this column, so assigning a plaintext
    /// code here would silently defeat the hashing rather than fail.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Saudi local time.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Saudi local time; null while the code is unused.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>How many wrong values have been submitted against this code. The
    /// code is invalidated once this passes the configured cap, so guessing it
    /// cannot be brute-forced.</summary>
    public int AttemptCount { get; set; }
}
