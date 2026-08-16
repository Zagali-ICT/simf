using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.Exhibitors;

/// <summary>An exhibiting company. Login accounts are provisioned under it afterwards,
/// each tracked as an <see cref="ExhibitorMembership"/>.</summary>
public sealed class Exhibitor : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }

    public ExhibitorTier? Tier { get; set; }

    public string? PhoneSecondary { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    /// <summary>The exhibitor's logo, in the one file store.
    ///
    /// <para>Until this column existed the link ran one way only: the
    /// <c>StoredFile</c> carried <c>OwnerEntityType</c>/<c>OwnerEntityId</c> back to
    /// this row and nothing here pointed at the file. That is a polymorphic pair no
    /// foreign key can constrain, so nothing stopped a file naming a row that had
    /// been deleted, and "which file is current" was decided in code rather than by
    /// the schema. <c>OwnerPointerSync</c> keeps this column in step for
    /// <c>FileService.ExhibitorLogo</c>.</para></summary>
    public Guid? LogoFileId { get; set; }
    public StoredFile? LogoFile { get; set; }
}
