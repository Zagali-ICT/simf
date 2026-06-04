namespace SIMF.Contracts.Exhibition;

/// <summary>D-199 — public booth list item (Mockup page 22). Only the
/// fields the visitor-facing exhibition page + 2D map need.</summary>
public sealed class PublicBoothSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? ExhibitorNameEn { get; set; }
    public string? ExhibitorNameAr { get; set; }
    public string? SectorEn { get; set; }
    public string? SectorAr { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
}

/// <summary>D-199 — public booth detail (adds the description paragraph).</summary>
public sealed class PublicBoothDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? ExhibitorNameEn { get; set; }
    public string? ExhibitorNameAr { get; set; }
    public string? SectorEn { get; set; }
    public string? SectorAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
}

/// <summary>D-199 — admin grid row. B1 — D-222: the exhibitor is now the
/// <see cref="ExhibitorId"/> relation (the CP resolves the name client-side from
/// the loaded exhibitor list, mirroring <see cref="HallId"/>).</summary>
public sealed class AdminBoothSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? SectorEn { get; set; }
    public Guid? HallId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>D-199 — admin full detail (every column incl. map position).
/// B1 — D-222: exhibitor = <see cref="ExhibitorId"/> relation + booth-officer
/// contact.</summary>
public sealed class AdminBoothDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? SectorEn { get; set; }
    public string? SectorAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>D-199 — admin create payload. B1 — D-222: exhibitor =
/// <see cref="ExhibitorId"/> relation + booth-officer contact.</summary>
public sealed class AdminCreateBoothRequest
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? SectorEn { get; set; }
    public string? SectorAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
}

/// <summary>D-199 — admin update payload. B1 — D-222: exhibitor =
/// <see cref="ExhibitorId"/> relation + booth-officer contact.</summary>
public sealed class AdminUpdateBoothRequest
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? SectorEn { get; set; }
    public string? SectorAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public bool IsActive { get; set; } = true;
}
