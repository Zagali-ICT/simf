using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use, time-limited code sent to someone, for verifying an email
/// address or resetting a password. One table serves both, distinguished by
/// <see cref="Purpose"/>.
/// </summary>
public class AccountCode
{
    public Guid Id { get; set; }

    /// <summary>The account the code was issued to, or null when it was issued
    /// to an attendee who holds none yet - see <see cref="UserProfileId"/>.
    /// Exactly one of the two is set.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The attendee the code was issued to, for the one flow that runs
    /// before an account exists: a badge holder proving control of an email so
    /// that an account can be created and linked to them.
    ///
    /// <para>A bare logical id and never a foreign key, because
    /// <c>UserProfile</c> lives in the other database. Null for every other
    /// code, which is issued to an account that already exists.</para></summary>
    public Guid? UserProfileId { get; set; }

    /// <summary>The address this code was sent to, pinned when it was issued,
    /// for the flow that verifies an address before any account exists.
    ///
    /// <para>It is what keeps verify-then-attach honest with no account to stash
    /// the address on: the completing request carries the code, never the
    /// address, so whoever holds a code cannot bind it to an address the code was
    /// never sent to. Null for codes issued to an account, which stash the
    /// pending address on the account itself.</para></summary>
    public string? PendingEmail { get; set; }

    public AccountCodePurpose Purpose { get; set; }

    public string Code { get; set; } = string.Empty;

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
