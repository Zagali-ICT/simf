using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One person who presents a <see cref="Session"/>, joined to their sessions
/// through <see cref="SessionSpeaker"/>.
///
/// <para>A speaker is a programme record before it is an account: most are
/// external guests who never hold a SIMF login, and everything the public profile
/// shows sits behind the two consent toggles below.</para>
/// </summary>
public class Speaker : BaseAuditEntity
{
    /// <summary>Unique, upper-cased, and the key the Excel grid import matches a
    /// sheet row on.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    /// <summary>The honorific or title line printed under the name, not a sort
    /// position: <see cref="DisplayOrder"/> is the sort position.</summary>
    public string? Rank { get; set; }
    public string? RankArabic { get; set; }

    /// <summary>ISO 3166-1 numeric country id, and the entity's only nationality
    /// field. Null when none is recorded.</summary>
    public int? CountryId { get; set; }

    /// <summary>A real foreign key: the country is in the same DbContext, so the
    /// public projection reads its name through this join instead of a second
    /// query per page.</summary>
    public Country? Country { get; set; }

    /// <summary>The <c>UserProfile</c> of a speaker who also holds a SIMF account,
    /// null for the external speakers who do not. A real foreign key despite there
    /// being no navigation for it: profiles live in the App database beside the
    /// speakers, not in Identity. Never surfaced publicly.</summary>
    public Guid? UserProfileId { get; set; }

    // The four long-form pairs behind the four bilingual tabs of the public
    // speaker profile.
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }

    public string? Qualifications { get; set; }
    public string? QualificationsArabic { get; set; }

    public string? TrainingExperience { get; set; }
    public string? TrainingExperienceArabic { get; set; }

    public string? Awards { get; set; }
    public string? AwardsArabic { get; set; }

    /// <summary>Shows the "Request meeting" affordance, and is checked again
    /// server-side before a request is accepted, so clearing it closes the endpoint
    /// as well as hiding the button. False by default.</summary>
    public bool AllowsMeetingRequests { get; set; }

    /// <summary>The consent gate over the URLs below: without it the public profile
    /// returns null for each of them and they stay admin-only. False by default, so
    /// a new speaker publishes nothing until an admin turns it on.</summary>
    public bool AllowsDataSharing { get; set; }

    public string? FacebookUrl { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? XUrl { get; set; }

    /// <summary>Captured on the admin form but published nowhere: unlike the other
    /// social URLs there is no Instagram slot on the public speaker contract.</summary>
    public string? InstagramUrl { get; set; }

    public string? WebsiteUrl { get; set; }

    // The identity-card block, the admin's contact sheet for the speaker. None of
    // it reaches the public projection, and there is deliberately no country in it:
    // CountryId above is the nationality. Latitude and Longitude are decimal
    // degrees.
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Vestigial: nothing writes it any more and the seed scripts leave it
    /// null. The photo comes from the speaker's active SpeakerPhoto asset in the
    /// StoredFile store; this path survives on the public contracts only as a
    /// fallback for rows that predate that store.</summary>
    public string? PhotoRelativePath { get; set; }

    /// <summary>Ascending sort position for the speakers list, ties broken by name.
    /// Indexed with IsActive, the pair those queries filter and order on.</summary>
    public int DisplayOrder { get; set; }
}
