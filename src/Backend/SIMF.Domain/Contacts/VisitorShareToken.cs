using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;

/// <summary>
/// SIMF-FDS-014 §5.4 (D-284, Track 2) — a visitor's dedicated, rotatable
/// "share my contact" token. Minted on demand the first time the visitor opens
/// <em>Share my contact</em>; resolving it returns a card projected live from
/// the owner's <c>UserProfile</c>. It is <b>separate from the entry QrId</b> so
/// scanning someone at the gate never harvests their card (the Q3 decision).
///
/// <para><see cref="UserId"/> is a <b>bare Guid logical FK</b> to
/// <c>SimfUser.Id</c> on the Identity DB — no EF navigation, no DB FK, no
/// cross-DB join (D-157). Rotating revokes the active token
/// (<see cref="IsActive"/> = false, <see cref="RevokedAt"/> set) and mints a new
/// one so a previously shared code stops resolving.</para>
/// </summary>
public sealed class VisitorShareToken : BaseAuditEntity
{
    /// <summary>Owning visitor — bare Guid logical FK to <c>SimfUser.Id</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>Opaque Crockford base32 token (≤32 chars, unique) — the
    /// shareable code encoded into the visitor's QR / share-intent.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>When the token was revoked on rotation; null while active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
