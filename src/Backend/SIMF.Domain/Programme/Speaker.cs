using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One programme speaker (SIMF-DAT-001 §5.4). D-153 enhances the basic
/// shape introduced by the <c>AddSpeakers</c> migration with: an int
/// nationality FK to the new <c>Country</c> table (replaces the prior
/// free-text <c>CountryCode</c>); an optional logical FK to a
/// <c>UserProfile</c> for speakers who also hold a SIMF account; full
/// Arabic counterparts for every long-form text (Bio / Qualifications
/// / TrainingExperience / Awards); two consent toggles
/// (<see cref="AllowsMeetingRequests"/>, <see cref="AllowsDataSharing"/>)
/// that drive what the public speaker page is allowed to surface; and
/// three social-profile URLs the speaker has opted to publish.
/// </summary>
public class Speaker : BaseAuditEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }

    /// <summary>D-153 — ISO 3166-1 numeric country id, logical FK to
    /// <c>SIMF.Domain.Common.Country.Id</c>. Optional (some speakers
    /// may have no recorded nationality). Indexed so the rare
    /// "speakers by country" admin query stays cheap.</summary>
    public int? CountryId { get; set; }

    /// <summary>D-153 / D-167 — when the speaker also holds a SIMF
    /// account, the owning <c>UserProfile.Id</c>. After D-167 moved
    /// <c>UserProfile</c> onto <c>SimfAppDbContext</c>, this is a real
    /// same-DB FK with <c>OnDelete.Restrict</c>. Null for external
    /// speakers who do not have a SIMF account.</summary>
    public Guid? UserProfileId { get; set; }

    public string? Bio { get; set; }
    public string? BioArabic { get; set; }

    public string? Qualifications { get; set; }
    public string? QualificationsArabic { get; set; }

    public string? TrainingExperience { get; set; }
    public string? TrainingExperienceArabic { get; set; }

    public string? Awards { get; set; }
    public string? AwardsArabic { get; set; }

    /// <summary>D-153 — when true, the public speaker page exposes the
    /// "Request meeting" affordance; false (default) hides it.</summary>
    public bool AllowsMeetingRequests { get; set; }

    /// <summary>D-153 — when true, the speaker has consented to having
    /// their bio + social URLs surfaced to other attendees via the
    /// in-app delegate-data exchange; false (default) keeps them
    /// admin-only.</summary>
    public bool AllowsDataSharing { get; set; }

    /// <summary>D-153 — optional Facebook profile URL the speaker has
    /// chosen to publish.</summary>
    public string? FacebookUrl { get; set; }

    /// <summary>D-153 — optional LinkedIn profile URL.</summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>D-153 — optional X (formerly Twitter) profile URL.</summary>
    public string? XUrl { get; set; }

    public string? PhotoRelativePath { get; set; }

    /// <summary>SIMF-FDS-014 (D-260) — optional link to the shared <c>Contact</c>
    /// directory record (logo / name / phones / social / website / location /
    /// country). Null until linked; multiple entities may reference the same
    /// Contact. The public projection prefers the Contact when set.</summary>
    public Guid? ContactId { get; set; }

    public int DisplayOrder { get; set; }
}
