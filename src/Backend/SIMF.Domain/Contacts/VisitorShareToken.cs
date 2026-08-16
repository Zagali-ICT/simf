// Tests: SIMF.Api.Tests/VisitorContactSharingTests.cs
using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;

/// <summary>
/// A visitor's rotatable "share my contact" token, deliberately separate from the entry
/// QR so scanning someone at a gate never harvests their contact card. Rotating revokes
/// the active token and mints a replacement, which is what stops an already-shared code
/// resolving.
/// </summary>
public sealed class VisitorShareToken : BaseAuditEntity
{
    /// <summary>A bare Guid: the user lives in the Identity database.</summary>
    public Guid UserId { get; set; }

    /// <summary>The shareable code encoded into the visitor's QR — an opaque
    /// Crockford base32 string — held AES-GCM encrypted at rest in the
    /// <c>enc:1:</c> form the profile's PII columns use.
    ///
    /// <para>Encrypted rather than hashed because the owner re-reads the code every
    /// time they reopen the share screen, which a one-way digest would make
    /// impossible: the sibling tokens (<c>RefreshToken</c>,
    /// <c>SecondFactorToken</c>, <c>MeetingActionToken</c>, <c>TotpRecoveryCode</c>)
    /// are shown once and never again, so <c>OpaqueToken.Hash</c> fits them and not
    /// this. Encrypted rather than left readable because the code is replay value a
    /// database reader does not otherwise hold: it resolves to a card carrying the
    /// mobile number, which this same database keeps as ciphertext, and the email,
    /// which lives on the Identity database entirely. Storing the code in the clear
    /// would hand a reader of this one table the very fields the rest of the schema
    /// is at pains to protect.</para>
    ///
    /// <para>Random-nonce encryption cannot be equality-queried, so the lookup keys
    /// off <see cref="TokenHash"/> instead; this column carries no index.</para></summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The keyed HMAC-SHA256 digest of the code — the deterministic lookup
    /// key a scanned code is resolved through, and the column carrying the
    /// uniqueness constraint. Keyed rather than a bare hash so a database leak
    /// cannot brute-force the small twelve-character code space back to plaintext
    /// offline. Required: a row exists only when there is a code behind it.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime? RevokedAt { get; set; }
}
