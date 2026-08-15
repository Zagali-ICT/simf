using SIMF.Domain.Common;

namespace SIMF.Domain.Profiles;

/// <summary>
/// Which identity document a <see cref="ProfileIdentityDocument"/> row holds.
///
/// <para>A NEW enum type rather than a new member on an existing one, which is
/// what the enum freeze allows: the frozen enums under
/// <c>SIMF.Common/Enums</c> are closed against rename and reorder, and appending
/// a whole new type touches none of them. It lives in the Domain rather than in
/// <c>SIMF.Common</c> because nothing serialises it: the wire keeps the three
/// separate <c>nationalId</c> / <c>iqamaNumber</c> / <c>passportNumber</c> keys
/// the shipped app decodes, and this discriminator never leaves the database.</para>
///
/// <para>Persisted as its NAME, not its ordinal (see the configuration), so a
/// later reorder cannot silently re-interpret stored rows as a different
/// document.</para>
/// </summary>
public enum IdentityDocumentKind
{
    /// <summary>Saudi national id: 10 digits starting with 1, Luhn-valid.</summary>
    NationalId = 0,

    /// <summary>Residency permit (إقامة): 10 digits starting with 2,
    /// Luhn-valid.</summary>
    Iqama = 1,

    /// <summary>A passport or any other travel / civil document. Length-checked
    /// only — passports carry no universal checksum.</summary>
    Passport = 2,
}

/// <summary>
/// One identity document belonging to one attendee.
///
/// <para><b>Why a child table and not one column plus a kind.</b> An attendee can
/// legitimately hold MORE THAN ONE document at once. The upsert validator's rule
/// is an OR, not an XOR — <c>UpsertUserProfileRequestValidator</c> requires
/// "either Iqama or Passport" and accepts both — and
/// <c>UserProfileService.UpsertMineAsync</c> already stores both when both are
/// supplied. A single number-plus-kind column would have to discard one of them,
/// and with it the guard that catches somebody re-registering under their other
/// document, which is the one thing the duplicate-identity rule exists to
/// prevent.</para>
///
/// <para><b>Why ONE unique index and not three.</b> The three per-kind filtered
/// uniques on <see cref="UserProfile"/> can only ever catch a repeat of the SAME
/// kind: a person registering once on a passport and again on an Iqama passes all
/// three. A single unique index over <see cref="NumberHash"/> — one column now
/// holding every document number's digest — catches the cross-kind repeat as
/// well, because both digests land in the same index.</para>
///
/// <para><b>Encryption and determinism, unchanged from the parent row.</b>
/// <see cref="Number"/> is the plaintext the PII value converter encrypts at rest
/// under a RANDOM nonce, so it can never be equality-queried or unique-indexed;
/// <see cref="NumberHash"/> is the deterministic keyed HMAC that can, and it
/// deliberately sits OUTSIDE that converter, because encrypting a digest destroys
/// the determinism the index depends on.</para>
///
/// <para><b>Not scoped to an edition.</b> The digest index is global. A returning
/// attendee keeps ONE profile row across editions
/// (<see cref="UserProfile.EditionYear"/> re-stamps it), so their documents are
/// updated in place rather than re-inserted, and there is no second registration
/// for a per-edition scope to permit.</para>
/// </summary>
public class ProfileIdentityDocument : BaseAuditEntity
{
    /// <summary>The attendee this document belongs to. A real intra-App-database
    /// foreign key with Cascade delete: the document has no meaning without its
    /// profile, and an orphaned digest would keep occupying the unique index and
    /// reject a legitimate re-registration for ever.</summary>
    public Guid ProfileId { get; set; }

    public UserProfile? Profile { get; set; }

    /// <summary>Which document this is. The discriminator that takes over
    /// <c>UserProfile.IsSaudi</c>'s job of choosing between the three
    /// columns.</summary>
    public IdentityDocumentKind Kind { get; set; }

    /// <summary>The document number as the registrant typed it, trimmed.
    /// Encrypted at rest by the PII value converter in
    /// <c>SimfAppDbContext.OnModelCreating</c>, which is why the column is sized
    /// to hold the <c>enc:1:</c> blob rather than a bare number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>The deterministic keyed-HMAC blind index of
    /// <see cref="Number"/> (64 hex chars), and the only column the
    /// duplicate-identity guard and its unique index can key off. Never
    /// encrypted — see the class remarks.</summary>
    public string NumberHash { get; set; } = string.Empty;
}
