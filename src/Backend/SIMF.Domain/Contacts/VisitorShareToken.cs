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

    /// <summary>The opaque Crockford base32 code encoded into the visitor's QR.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTime? RevokedAt { get; set; }
}
