using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Contacts;

namespace SIMF.Domain.Sponsors;

/// <summary>
/// D-199 (Mockup page 23, "الرعاة" / Sponsors) — one event sponsor shown on the
/// public sponsors screen and managed from the Control Panel. Sponsors are
/// grouped on the public surface by <see cref="Tier"/> (Platinum first) and,
/// within a tier, ordered by <see cref="DisplayOrder"/> ascending then
/// <see cref="NameArabic"/>. Soft-deleted via <see cref="IsActive"/> /
/// <see cref="Deactivate"/> — the public list filters <c>IsActive == true</c>.
/// </summary>
public sealed class Sponsor: BaseAuditEntity
{ 

    /// <summary>English display name (1–256 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars) — the primary surface on the
    /// mobile app and public website.</summary>
    public string NameArabic { get; set; } = string.Empty;


    /// <summary>Sponsorship tier. Drives both the grouping heading and the
    /// top-level ordering on the public screen.</summary>
    public SponsorTier Tier { get; set; } = SponsorTier.Bronze;

    /// <summary>Sort key within a tier — ascending. Tie-broken by
    /// <see cref="NameArabic"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Relative path to the sponsor's logo asset (≤256 chars), resolved
    /// against the static asset root. Optional — never an absolute URL. Retained
    /// per SIMF-FDS-014 (D-260): the entity keeps its own inline logo as the
    /// fallback; when a <see cref="Contact"/> is linked the public projection
    /// prefers the Contact's logo (D-281).</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Optional outbound link to the sponsor's website (≤512 chars).</summary>
    public string? Url { get; set; }

    /// <summary>D-432 — an optional short tagline / strapline shown under the
    /// sponsor name on the public screen (≤256 chars, bilingual). The Figma
    /// frame 922:2824 shows a line such as "الراعي الاستراتيجي · شريك التحول
    /// الدفاعي". Optional; the public projection omits it when blank.</summary>
    public string? Tagline { get; set; }

    /// <summary>D-432 — Arabic tagline (≤256 chars). The primary surface.</summary>
    public string? TaglineArabic { get; set; }

    /// <summary>Wave 3 (Figma 1439:11826) — the full "نبذة عن الراعي" about
    /// paragraph on the sponsor-detail screen (distinct from the short
    /// <see cref="Tagline"/>). Bilingual; ≤2048. Optional. Additive (D-219).</summary>
    public string? About { get; set; }
    public string? AboutArabic { get; set; }


    /// <summary>SIMF-FDS-014 (D-260) — optional link to the shared <c>Contact</c>
    /// directory record (logo / name / phones / social / website / location /
    /// country). Null until linked; multiple entities may reference the same
    /// Contact. The public projection prefers the Contact when set.</summary>
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }//Add FK



    
}
