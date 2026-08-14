using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;

/// <summary>
/// A visitor's rotatable "share my contact" token, minted the first time they
/// open the share screen. Resolving one returns a card projected live from the
/// owner's profile.
///
/// <para>Deliberately separate from the entry QR, so scanning someone at a gate
/// never harvests their contact card. Rotating revokes the active token and mints
/// a replacement, which is what makes a code already shared stop
/// resolving.</para>
/// </summary>
public sealed class VisitorShareToken : BaseAuditEntity
{
    /// <summary>The owning visitor. A bare Guid: the user lives in the Identity
    /// database, so there is no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>The shareable code encoded into the visitor's QR, an opaque
    /// Crockford base32 string, unique across the table.
    ///
    /// <para>Stored in plaintext, and deliberately so — this is the one token in
    /// the system that does not follow the <c>OpaqueToken.Hash</c> convention
    /// <c>RefreshToken</c>, <c>SecondFactorToken</c>, <c>MeetingActionToken</c> and
    /// <c>TotpRecoveryCode</c> all use. Two reasons. The owner re-reads this value
    /// every time they reopen the share screen, so a one-way hash would mean nobody
    /// could ever see their own QR again after minting it. And it guards nothing an
    /// attacker would not already hold: the card it resolves to is projected live
    /// from <c>UserProfile</c> in this same database, so a reader who has this table
    /// has the contact details directly and does not need the code. Encrypting it
    /// would buy a schema change and a blind index for no reduction in exposure.</para></summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>When rotation revoked the token; null while it is active.</summary>
    public DateTime? RevokedAt { get; set; }
}
