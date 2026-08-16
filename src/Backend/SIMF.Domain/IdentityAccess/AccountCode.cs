// Tests: SIMF.Api.Tests/AuthFlow.cs (recovers the plaintext from Code by
//        brute force, proving the column holds a hash) and
//        SIMF.Api.Tests/RetentionPurgeServiceTests.cs (expiry purge).
using SIMF.Common.Enums;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use, time-limited code emailed to someone. One table serves every
/// such flow, distinguished by <see cref="Purpose"/>: verifying an address at
/// sign-up, resetting a password, the visitor sign-in second factor, activating
/// a badge, stepping up before a biometric key is enrolled, and confirming a new
/// login address. The row holds only a keyed hash of the code
/// (<see cref="Code"/>) — the code itself is emailed and never persisted.
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

    /// <summary>The keyed HMAC of the code, never the code itself. Written only by
    /// <c>AccountCodeHasher.Hash</c>, and only ever compared against another hash of
    /// a submitted value — nothing queries this column, so assigning a plaintext
    /// code here would silently defeat the hashing rather than fail.</summary>
    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    /// <summary>Wrong submissions; the code is invalidated past the configured cap.</summary>
    public int AttemptCount { get; set; }
}
