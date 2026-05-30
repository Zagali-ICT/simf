namespace SIMF.Contracts.PublicRelations;

// -- Public (anonymous) read projection --

/// <summary>D-199 (Mockup page 31) — one item in the public media-partner list.</summary>
public sealed record PublicMediaPartnerItem(
    Guid Id,
    string NameEn,
    string NameAr,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder);

/// <summary>D-199 (Mockup page 31) — the public media-partner list payload
/// (active rows only, ordered by DisplayOrder then NameAr).</summary>
public sealed record PublicMediaPartners(IReadOnlyList<PublicMediaPartnerItem> Items);

// -- Admin CRUD projections --

/// <summary>D-199 — admin list-row projection of a media partner.</summary>
public sealed record AdminMediaPartnerSummary(
    Guid Id,
    string NameEn,
    string NameAr,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>D-199 — admin detail projection of a media partner.</summary>
public sealed record AdminMediaPartnerDetail(
    Guid Id,
    string NameEn,
    string NameAr,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-199 — create payload (Id is server-assigned).</summary>
public sealed record AdminCreateMediaPartnerRequest(
    string NameEn,
    string NameAr,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder);

/// <summary>D-199 — update payload (Id travels in the route).</summary>
public sealed record AdminUpdateMediaPartnerRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
