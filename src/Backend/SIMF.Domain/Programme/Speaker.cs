using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.Programme;

/// <summary>One person who presents a <see cref="Session"/>, joined through
/// <see cref="SessionSpeaker"/>. Most are external guests with no SIMF login.</summary>
public class Speaker : BaseAuditEntity
{
    /// <summary>Uppercased on write, and the key the Excel import matches on.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    /// <summary>The honorific line printed under the name, not a sort position.</summary>
    public string? Rank { get; set; }
    public string? RankArabic { get; set; }

    /// <summary>ISO 3166-1 numeric, and the only nationality field.</summary>
    public int? CountryId { get; set; }

    public Country? Country { get; set; }

    /// <summary>Set for a speaker who also holds a SIMF account. Never surfaced publicly.</summary>
    public Guid? UserProfileId { get; set; }

    public string? Bio { get; set; }
    public string? BioArabic { get; set; }

    public string? Qualifications { get; set; }
    public string? QualificationsArabic { get; set; }

    public string? TrainingExperience { get; set; }
    public string? TrainingExperienceArabic { get; set; }

    public string? Awards { get; set; }
    public string? AwardsArabic { get; set; }

    /// <summary>Re-checked server-side, so clearing it closes the endpoint too.</summary>
    public bool AllowsMeetingRequests { get; set; }

    /// <summary>Consent gate over the URLs below: without it the public profile
    /// returns null for each of them.</summary>
    public bool AllowsDataSharing { get; set; }

    public string? FacebookUrl { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? XUrl { get; set; }

    /// <summary>Captured on the admin form but published nowhere: the public
    /// speaker contract has no Instagram slot.</summary>
    public string? InstagramUrl { get; set; }

    public string? WebsiteUrl { get; set; }

    // The admin's contact sheet; none of it reaches the public projection.
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }


    /// <summary>Ascending, ties broken by name.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>The speaker portrait, in the one file store.
    ///
    /// <para>Until this column existed the link ran one way only: the
    /// <c>StoredFile</c> carried <c>OwnerEntityType</c>/<c>OwnerEntityId</c> back to
    /// this row and nothing here pointed at the file. That is a polymorphic pair no
    /// foreign key can constrain, so nothing stopped a file naming a row that had
    /// been deleted, and "which file is current" was decided in code rather than by
    /// the schema. <c>OwnerPointerSync</c> keeps this column in step for
    /// <c>FileService.SpeakerPhoto</c>.</para></summary>
    public Guid? PhotoFileId { get; set; }
    public StoredFile? PhotoFile { get; set; }
}
