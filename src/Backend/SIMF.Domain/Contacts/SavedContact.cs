using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;

/// <summary>
/// SIMF-FDS-014 §5.6 (D-284, Track 2) — a contact a visitor saved to their
/// <em>My Contacts</em> after scanning / resolving another visitor's share
/// token. Both user references are <b>bare Guid logical FKs</b> to
/// <c>SimfUser.Id</c> on the Identity DB — no EF navigation, no DB FK, no
/// cross-DB join (D-157).
///
/// <para><b>No snapshot of the subject's PII is stored</b> (D-157 forbids
/// extending the audit-snapshot copy pattern to live data): the card is
/// resolved on read from the subject's <c>UserProfile</c> plus a permitted
/// email round-trip. If the subject is later deactivated, the saved row shows a
/// limited / unavailable card. Saving is idempotent per
/// (<see cref="OwnerUserId"/>, <see cref="SubjectUserId"/>); removing is a
/// soft-delete (<see cref="BaseAuditEntity.Deactivate"/>). <c>SavedAt</c> is the
/// inherited <see cref="BaseAuditEntity.CreatedAt"/>.</para>
/// </summary>
public sealed class SavedContact : BaseAuditEntity
{
    /// <summary>The visitor who saved the contact — bare Guid → <c>SimfUser.Id</c>.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>The saved (subject) visitor — bare Guid → <c>SimfUser.Id</c>.</summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>Optional free-text note the owner attached (≤512 chars).</summary>
    public string? Note { get; set; }
}
