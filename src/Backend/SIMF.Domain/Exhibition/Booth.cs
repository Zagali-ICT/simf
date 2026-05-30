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
public class Booth
{
    public Guid Id { get; set; }

    /// <summary>Short booth code shown in the card header (e.g. "A-12").
    /// Unique across active and inactive booths.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English booth name (1–128 chars), e.g. "Advanced Naval
    /// Technologies".</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic booth name (1–128 chars).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English exhibitor / company name (≤ 256 chars), e.g.
    /// "Saudi Arabian Military Industries". Optional.</summary>
    public string? ExhibitorNameEn { get; set; }

    /// <summary>Arabic exhibitor / company name (≤ 256 chars). Optional.</summary>
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

    /// <summary>Soft-delete flag. List/read endpoints filter on this.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Reverts <see cref="IsActive"/> to false — the soft-delete
    /// operation used by the admin DELETE endpoint.</summary>
    public void Deactivate() => IsActive = false;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
