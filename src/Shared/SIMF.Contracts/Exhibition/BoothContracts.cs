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

/// <summary>D-199 — admin grid row.</summary>
public sealed class AdminBoothSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? ExhibitorNameEn { get; set; }
    public string? SectorEn { get; set; }
    public Guid? HallId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>D-199 — admin full detail (every column incl. map position).</summary>
public sealed class AdminBoothDetail
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
    public bool IsActive { get; set; }
}

/// <summary>D-199 — admin create payload.</summary>
public sealed class AdminCreateBoothRequest
{
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

/// <summary>D-199 — admin update payload.</summary>
public sealed class AdminUpdateBoothRequest
{
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
    public bool IsActive { get; set; } = true;
}
