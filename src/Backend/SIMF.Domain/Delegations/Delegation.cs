using SIMF.Domain.Common;

namespace SIMF.Domain.Delegations;
//remove this perminitally
/// <summary>
/// D-174 (gap doc G11, Mockup page 21) — one delegation attending the
/// forum (e.g. "وفد البحرية الملكية" — Royal Navy Delegation, 8 members).
/// The mobile app's "Delegations" screen lists these grouped by
/// <see cref="IsPriority"/> (أولوية قصوى) with chip filters for "All",
/// "Priority" (ذو أولوية), and "International" (دولي).
/// </summary>
public sealed class Delegation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>English display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name — the primary surface on the mobile app.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Optional FK to <see cref="Country"/> on the App DB.
    /// Null when the delegation is multi-country (e.g. a joint NATO
    /// observer team) or unaffiliated.</summary>
    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    /// <summary>Number of members in the delegation. Shown in the card
    /// subtitle ("المملكة المتحدة · 8 أعضاء").</summary>
    public int MemberCount { get; set; }

    /// <summary>True when the delegation carries the "أ.أولى" (first-
    /// priority) badge on the card. The mobile-app screen groups
    /// priority delegations under a dedicated heading.</summary>
    public bool IsPriority { get; set; }

    /// <summary>True for non-host-country delegations — drives the
    /// "International" filter chip on the mobile-app screen.</summary>
    public bool IsInternational { get; set; }

    /// <summary>Sort key on the public list — ascending. Tie-broken
    /// by <see cref="NameArabic"/>.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
