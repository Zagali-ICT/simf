using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibition;

/// <summary>
/// D-199 — one exhibition booth shown on Mockup page 22 (Booths) and fed
/// into the 2D venue map. Bilingual like <c>Hall</c> / <c>Country</c>
/// (NameEn / NameAr), soft-deleted via <see cref="IsActive"/>, and audited
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
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic booth name (1–128 chars).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>B1 — D-222: the curated exhibitor, a real FK to
    /// <c>Company.Id</c> (same App DB; only <c>CompanyType.Exhibitor</c>
    /// companies are valid). This is the source of truth for the exhibitor;
    /// the public projection fills <see cref="ExhibitorNameEn"/> /
    /// <see cref="ExhibitorNameAr"/> from the linked company when set.
    /// Nullable — a booth may exist before its company is provisioned.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>B1 — D-222: booth-officer contact name (≤ 256 chars). Optional.</summary>
    public string? OfficerName { get; set; }

    /// <summary>B1 — D-222: booth-officer phone (≤ 32 chars). Optional.</summary>
    public string? OfficerPhone { get; set; }

    /// <summary>B1 — D-222: booth-officer email (≤ 320 chars). Optional.</summary>
    public string? OfficerEmail { get; set; }

    /// <summary>SIMF-FDS-014 (D-260 / OI-1) — optional link to the shared
    /// <c>Contact</c> directory record for the booth <b>officer</b> (a person,
    /// distinct from the exhibitor company, which is linked via
    /// <see cref="CompanyId"/>). Null until linked; multiple entities may
    /// reference the same Contact.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>English exhibitor / company name (≤ 256 chars). Legacy free-text
    /// fallback retained for the public wire contract (D-219) and pre-D-222
    /// rows; new booths source the exhibitor from <see cref="CompanyId"/>. Not
    /// settable from the admin write surface any more.</summary>
    public string? ExhibitorNameEn { get; set; }

    /// <summary>Arabic exhibitor / company name (≤ 256 chars). Legacy free-text
    /// fallback — see <see cref="ExhibitorNameEn"/>.</summary>
    public string? ExhibitorNameAr { get; set; }

    /// <summary>English sector tag shown in the card header (≤ 128 chars),
    /// e.g. "Defense Systems". Optional.</summary>
    public string? SectorEn { get; set; }

    /// <summary>Arabic sector tag (≤ 128 chars). Optional.</summary>
    public string? SectorAr { get; set; }

    /// <summary>English booth description paragraph (≤ 2048 chars). Optional.</summary>
    public string? DescriptionEn { get; set; }

    /// <summary>Arabic booth description paragraph (≤ 2048 chars). Optional.</summary>
    public string? DescriptionAr { get; set; }

    /// <summary>D-199 — optional real FK to <c>Hall.Id</c> (same App DB).
    /// Null when the booth has not yet been placed in a hall/zone.</summary>
    public Guid? HallId { get; set; }

    /// <summary>D-199 — booth X position on the 2D venue map. Optional until
    /// the booth is placed.</summary>
    public double? MapX { get; set; }

    /// <summary>D-199 — booth Y position on the 2D venue map. Optional until
    /// the booth is placed.</summary>
    public double? MapY { get; set; }
}
