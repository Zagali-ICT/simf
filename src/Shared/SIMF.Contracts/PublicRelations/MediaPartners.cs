namespace SIMF.Contracts.PublicRelations;

// -- Public (anonymous) read projection --

/// <summary>D-199 (Mockup page 31) — one item in the public media-partner list.</summary>
public sealed record PublicMediaPartnerItem(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder);

/// <summary>D-199 (Mockup page 31) — the public media-partner list payload
/// (active rows only, ordered by DisplayOrder then NameArabic).</summary>
public sealed record PublicMediaPartners(IReadOnlyList<PublicMediaPartnerItem> Items);

// -- Admin CRUD projections --

/// <summary>D-199 — admin list-row projection of a media partner.</summary>
public sealed record AdminMediaPartnerSummary(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>D-199 — admin detail projection of a media partner.
/// SIMF-FDS-014 (D-281): carries the optional shared-<c>Contact</c> link.</summary>
public sealed record AdminMediaPartnerDetail(
    Guid Id,
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ContactId);

/// <summary>D-199 — create payload (Id is server-assigned). SIMF-FDS-014
/// (D-281): <c>ContactId</c> optionally links a shared <c>Contact</c>.</summary>
public sealed record AdminCreateMediaPartnerRequest(
    string Name,
    string NameArabic,
    string? LogoRelativePath,
    string? Url,
    int DisplayOrder,
    Guid? ContactId = null);

/// <summary>D-199 — update payload (Id travels in the route).</summary>
public sealed record AdminUpdateMediaPartnerRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? LogoRelativePath { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>SIMF-FDS-014 (D-281) — optional link to a shared <c>Contact</c>
    /// directory record. Must reference an existing active contact.</summary>
    public Guid? ContactId { get; set; }

    public bool IsActive { get; set; } = true;
}
