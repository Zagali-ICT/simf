using SIMF.Domain.Common;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Programme;

namespace SIMF.Domain.Exhibition;

/// <summary>One stand on the exhibition floor. The booth is the space and the
/// <see cref="Exhibitor"/> is the company standing in it; a booth is coded before its
/// exhibitor is signed, so the exhibitor-facing fields are nullable by design.</summary>
public class Booth : BaseAuditEntity
{
    /// <summary>Short floor code such as "A-12". Upper-cased on write, and unique
    /// across active and inactive booths alike.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The public projection reads the linked <c>Exhibitor.Name</c>, falling back
    /// to <see cref="ExhibitorName"/> only when this is null, and hides the booth entirely
    /// when the exhibitor is inactive even though the booth's own IsActive is untouched.</summary>
    public Guid? ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    // The person on the stand. The Officer prefix is load-bearing: Name and
    // NameArabic above are the BOOTH's own, and the officer is a different party.
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? OfficerNameArabic { get; set; }
    public string? OfficerPhoneSecondary { get; set; }
    public string? OfficerWebsite { get; set; }
    public string? OfficerFacebookUrl { get; set; }
    public string? OfficerXUrl { get; set; }
    public string? OfficerLinkedInUrl { get; set; }
    public string? OfficerInstagramUrl { get; set; }
    public string? OfficerCity { get; set; }
    public string? OfficerCityArabic { get; set; }

    /// <summary>Degrees, set together or not at all. They pin the officer, not the
    /// booth, whose own place on the venue map is <see cref="MapX"/> / <see cref="MapY"/>.</summary>
    public double? OfficerLatitude { get; set; }
    public double? OfficerLongitude { get; set; }

    public int? OfficerCountryId { get; set; }
    public Country? OfficerCountry { get; set; }

    // Vestigial free text from before exhibitors were curated rows. Nothing writes
    // them, and the public projection reads them only when ExhibitorId is null.
    public string? ExhibitorName { get; set; }
    public string? ExhibitorNameArabic { get; set; }

    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    // Captured and published, but no map is drawn from them: the map the app renders
    // is built from VenueMapNode rows, which carry their own X / Y.
    public double? MapX { get; set; }
    public double? MapY { get; set; }
}
