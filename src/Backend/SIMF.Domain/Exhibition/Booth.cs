using SIMF.Domain.Common;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Programme;

namespace SIMF.Domain.Exhibition;

/// <summary>
/// D-199 — one exhibition booth shown on Mockup page 22 (Booths) and fed
/// into the 2D venue map. Bilingual like <c>Hall</c> / <c>Country</c>
/// (Name / NameArabic), soft-deleted via <see cref="IsActive"/>, and audited
/// through the row-audit trail by the admin service.
///
/// <para><see cref="MapX"/> / <see cref="MapY"/> are the booth's normalised
/// position on the 2D venue map (the "View on Map" affordance in the
/// mockup). They are optional because a booth can be created before its map
/// placement is decided.</para>
///
/// <para><see cref="HallId"/> is an optional real FK to <c>Hall.Id</c>
/// (same App DbContext) — a booth may sit inside a hall/zone, or be null
/// when it has not been placed yet.</para>
/// </summary>
/// 


//Add exibtor as company and sponser
//then exibtorBooth
public class Booth : BaseAuditEntity
{
    /// <summary>Short booth code shown in the card header (e.g. "A-12").
    /// Unique across active and inactive booths.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English booth name (1–128 chars), e.g. "Advanced Naval
    /// Technologies".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic booth name (1–128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>B1 — D-222: the curated exhibitor, a real FK to
    /// <c>Exhibitor.Id</c> (same App DB). This is the source of truth for the
    /// exhibitor; the public projection fills <see cref="ExhibitorName"/> /
    /// <see cref="ExhibitorNameArabic"/> from the linked exhibitor when set.
    /// Nullable — a booth may exist before its exhibitor is provisioned.</summary>
    public Guid? ExhibitorId { get; set; }

    /// <summary>A5 — navigation for <see cref="ExhibitorId"/> (same FK), so a
    /// projection reads every exhibitor field through one join instead of a
    /// correlated subquery per field.</summary>
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>B1 — D-222: booth-officer contact name (≤ 256 chars). Optional.</summary>
    public string? OfficerName { get; set; }

    /// <summary>B1 — D-222: booth-officer phone (≤ 32 chars). Optional.</summary>
    public string? OfficerPhone { get; set; }

    /// <summary>B1 — D-222: booth-officer email (≤ 320 chars). Optional.</summary>
    public string? OfficerEmail { get; set; }

    /// <summary>Arabic booth-officer name (≤ 256 chars). Optional. Prefixed
    /// <c>Officer</c> because the bare <see cref="NameArabic"/> is the BOOTH's
    /// own Arabic name; the officer is a distinct person.</summary>
    public string? OfficerNameArabic { get; set; }

    /// <summary>Booth-officer secondary phone (≤ 32 chars). Optional.</summary>
    public string? OfficerPhoneSecondary { get; set; }

    /// <summary>Booth-officer website URL (≤ 512 chars). Optional.</summary>
    public string? OfficerWebsite { get; set; }

    /// <summary>Booth-officer Facebook URL (≤ 256 chars). Optional.</summary>
    public string? OfficerFacebookUrl { get; set; }

    /// <summary>Booth-officer X (Twitter) URL (≤ 256 chars). Optional.</summary>
    public string? OfficerXUrl { get; set; }

    /// <summary>Booth-officer LinkedIn URL (≤ 256 chars). Optional.</summary>
    public string? OfficerLinkedInUrl { get; set; }

    /// <summary>Booth-officer Instagram URL (≤ 256 chars). Optional.</summary>
    public string? OfficerInstagramUrl { get; set; }

    /// <summary>Booth-officer city, English (≤ 128 chars). Optional.</summary>
    public string? OfficerCity { get; set; }

    /// <summary>Booth-officer city, Arabic (≤ 128 chars). Optional.</summary>
    public string? OfficerCityArabic { get; set; }

    /// <summary>Booth-officer map latitude. Optional.</summary>
    public double? OfficerLatitude { get; set; }

    /// <summary>Booth-officer map longitude. Optional.</summary>
    public double? OfficerLongitude { get; set; }

    /// <summary>Booth-officer country, an optional logical FK to
    /// <c>Country.Id</c> (same App DB). Null until set.</summary>
    public int? OfficerCountryId { get; set; }

    /// <summary>A5 — navigation for <see cref="OfficerCountryId"/> (same FK):
    /// the booth officer's country.</summary>
    public Country? OfficerCountry { get; set; }

    /// <summary>English exhibitor / company name (≤ 256 chars). Legacy free-text
    /// fallback retained for the public wire contract (D-219) and pre-D-222
    /// rows; new booths source the exhibitor from <see cref="ExhibitorId"/>. Not
    /// settable from the admin write surface any more.</summary>
    public string? ExhibitorName { get; set; }

    /// <summary>Arabic exhibitor / company name (≤ 256 chars). Legacy free-text
    /// fallback — see <see cref="ExhibitorName"/>.</summary>
    public string? ExhibitorNameArabic { get; set; }

    /// <summary>English sector tag shown in the card header (≤ 128 chars),
    /// e.g. "Defense Systems". Optional.</summary>
    public string? Sector { get; set; }

    /// <summary>Arabic sector tag (≤ 128 chars). Optional.</summary>
    public string? SectorArabic { get; set; }

    /// <summary>English booth description paragraph (≤ 2048 chars). Optional.</summary>
    public string? Description { get; set; }

    /// <summary>Arabic booth description paragraph (≤ 2048 chars). Optional.</summary>
    public string? DescriptionArabic { get; set; }

    /// <summary>D-199 — optional real FK to <c>Hall.Id</c> (same App DB).
    /// Null when the booth has not yet been placed in a hall/zone.</summary>
    public Guid? HallId { get; set; }

    /// <summary>A5 — navigation for <see cref="HallId"/> (same FK): the hall the
    /// booth sits in.</summary>
    public Hall? Hall { get; set; }

    /// <summary>D-199 — booth X position on the 2D venue map. Optional until
    /// the booth is placed.</summary>
    public double? MapX { get; set; }

    /// <summary>D-199 — booth Y position on the 2D venue map. Optional until
    /// the booth is placed.</summary>
    public double? MapY { get; set; }
}
