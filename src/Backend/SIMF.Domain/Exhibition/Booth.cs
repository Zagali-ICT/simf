using SIMF.Domain.Common;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Programme;

namespace SIMF.Domain.Exhibition;

/// <summary>
/// One stand on the exhibition floor: a coded, bilingual space that an
/// <see cref="Exhibitor"/> occupies, optionally sited inside a <see cref="Hall"/>.
///
/// <para>The booth is the space and the exhibitor is the company standing in it;
/// neither owns the other. A booth is laid out and coded before its exhibitor is
/// signed, so the exhibitor-facing fields here are nullable by design rather than
/// by oversight.</para>
/// </summary>
public class Booth : BaseAuditEntity
{
    /// <summary>Short floor code such as "A-12". Upper-cased on write, and unique
    /// across every booth active or not, so deactivating a booth does not free its
    /// code for reuse.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The curated exhibitor, and the source of truth for its name: the
    /// public projection reads the linked <c>Exhibitor.Name</c> and falls back to
    /// <see cref="ExhibitorName"/> only when this is null. Deactivating the
    /// exhibitor also hides the booth from the public read, even though the
    /// booth's own IsActive is untouched.</summary>
    public Guid? ExhibitorId { get; set; }

    /// <summary>A real foreign key: the exhibitor lives in the same DbContext, so
    /// a projection reads every exhibitor field through one join.</summary>
    public Exhibitor? Exhibitor { get; set; }

    // The booth officer: the person on the stand, whose contact card is inlined
    // here rather than held in a shared directory. All optional, so an unstaffed
    // booth carries none of it. The Officer prefix is load-bearing: Name and
    // NameArabic above are the BOOTH's own names, and the officer is a different
    // party with their own name, city and country.
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

    /// <summary>Degrees, set together or not at all, and rejected outside -90..90
    /// and -180..180 by the admin service. They pin the officer, not the booth:
    /// the booth's own place on the venue map is <see cref="MapX"/> /
    /// <see cref="MapY"/>.</summary>
    public double? OfficerLatitude { get; set; }
    public double? OfficerLongitude { get; set; }

    /// <summary>A real foreign key: the country lookup lives in the same
    /// DbContext. Must point at an active country.</summary>
    public int? OfficerCountryId { get; set; }
    public Country? OfficerCountry { get; set; }

    // Vestigial: the exhibitor's name as free text, from before exhibitors were
    // curated rows. Nothing writes them any more (the admin create and update
    // paths do not accept them), and the public projection reads them only when
    // ExhibitorId is null. They stay because rows created before the link still
    // carry their only exhibitor name here.
    public string? ExhibitorName { get; set; }
    public string? ExhibitorNameArabic { get; set; }

    /// <summary>Free-text industry label such as "Defence Systems". Not a lookup,
    /// so two booths can spell the same sector differently.</summary>
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    /// <summary>A real foreign key: the hall lives in the same DbContext. Null
    /// means the booth is coded but not yet sited in a hall or zone.</summary>
    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    // The booth's own place on the 2D venue map. The Control Panel captures the
    // pair and the public API publishes it, but no map is drawn from it: the map
    // the app renders is built from VenueMapNode rows, which carry their own
    // X / Y and point back at the booth. Neither column has an enforced range or
    // unit. A booth that a venue-map node still marks cannot be deactivated; the
    // admin service rejects that with a 409 so the node is removed first.
    public double? MapX { get; set; }
    public double? MapY { get; set; }
}
