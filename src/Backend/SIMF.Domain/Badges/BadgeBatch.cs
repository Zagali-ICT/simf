using SIMF.Domain.Common;

namespace SIMF.Domain.Badges;

/// <summary>
/// One bulk order: the badges minted together, kept so they can be managed as a
/// unit afterwards — the QR pack re-emailed, the order topped up, or the whole
/// thing revoked. Every attendee carries a back-reference to its row here.
///
/// <para>Soft-deleted rather than removed, so those back-references always
/// resolve; revoking an order deactivates it.</para>
/// </summary>
public class BadgeBatch : BaseAuditEntity
{
    /// <summary>Who the order is for, as a person would say it — "Ministry of
    /// Interior Team". Distinct from <see cref="Items"/>, which say what is in
    /// it: an order identified only by its counts is unrecognisable in a list
    /// once there are several the same size.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The Arabic twin of <see cref="Name"/>. Arabic is the primary
    /// language of the estate, so an order named only in English reads as a gap
    /// on every Arabic screen.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The order that owns everyone who arrived without one: a direct
    /// registration, a walk-in, or an exhibition-desk capture. Seeded, fixed, and
    /// referenced with <c>Restrict</c> so it can never be deleted out from under
    /// them.
    ///
    /// <para>A constant id rather than a lookup by name, so the seeder is
    /// idempotent and nothing depends on the display text staying put.</para>
    /// </summary>
    public static readonly Guid DirectRegistrationId =
        new("0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0");
    /// <summary>What the order holds, one line per profile type. This replaced a
    /// rendered <c>CountsSummary</c> string ("VIP × 3 + Normal × 2") and a
    /// <c>TotalCount</c> cache of the same fact: both are composed on READ from
    /// these lines joined to the LIVE profile-type names, so a renamed tier
    /// corrects every historical order instead of leaving each one labelled with
    /// the name it carried at mint time. See <see cref="BadgeBatchItem"/>.</summary>
    public ICollection<BadgeBatchItem> Items { get; set; } = new List<BadgeBatchItem>();

    /// <summary>True when every badge in the batch was flagged as a delegation
    /// member.</summary>
    public bool IsDelegate { get; set; }

    /// <summary>The organiser address the QR pack went to at generate time, which
    /// a re-email pre-fills. Null when the batch was generated without an
    /// email.</summary>
    public string? RecipientEmail { get; set; }
}
