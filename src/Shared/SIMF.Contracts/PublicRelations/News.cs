namespace SIMF.Contracts.PublicRelations;

// ---------------------------------------------------------------------------
// D-199 — News / Media-Centre contracts (Mockup screen 29 / 29b).
// Public payloads are the app / website shape; admin payloads drive the CP
// grid + edit modal. Lives in the existing SIMF.Contracts.PublicRelations
// namespace alongside the Invitation contracts.
// ---------------------------------------------------------------------------

/// <summary>One card in the public News feed (Mockup screen 29). Carries the
/// teaser excerpt shown on the card; the full body is fetched via the detail
/// endpoint when a card is opened.</summary>
public sealed record PublicNewsListItem(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string? ExcerptEn,
    string? ExcerptAr,
    string CategoryEn,
    string CategoryAr,
    string? ImageRelativePath,
    DateTimeOffset PublishedAt);

/// <summary>A page of the public News feed. Self-contained paged envelope
/// (mirrors the <c>PublicDelegations</c> wrapper-record style rather than
/// inventing a shared generic) so paging metadata travels with the items.</summary>
public sealed record PublicNewsPage(
    IReadOnlyList<PublicNewsListItem> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>Full public article (Mockup screen 29b) including the body.</summary>
public sealed record PublicNewsArticle(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string BodyEn,
    string BodyAr,
    string CategoryEn,
    string CategoryAr,
    string? ImageRelativePath,
    DateTimeOffset PublishedAt);

/// <summary>One row in the admin News grid (CP). The grid and detail share the
/// summary except the detail adds the long-form body + excerpt + image. Admin
/// reads show every row regardless of <c>IsActive</c> / publish window so
/// editors can manage drafts and reactivate soft-deleted items.</summary>
public sealed record AdminNewsSummary(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string CategoryEn,
    string CategoryAr,
    DateTimeOffset PublishedAt,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Full article detail for the CP Details / Edit modal.</summary>
public sealed record AdminNewsDetail(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string? ExcerptEn,
    string? ExcerptAr,
    string BodyEn,
    string BodyAr,
    string CategoryEn,
    string CategoryAr,
    string? ImageRelativePath,
    DateTimeOffset PublishedAt,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Create payload (admin). Mirrors <c>CreateDelegationRequest</c> shape.</summary>
public sealed class CreateNewsRequest
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? ExcerptEn { get; set; }
    public string? ExcerptAr { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string CategoryEn { get; set; } = string.Empty;
    public string CategoryAr { get; set; } = string.Empty;
    public string? ImageRelativePath { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Update payload (admin). Adds <c>IsActive</c> to the create shape.</summary>
public class UpdateNewsRequest
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? ExcerptEn { get; set; }
    public string? ExcerptAr { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string CategoryEn { get; set; } = string.Empty;
    public string CategoryAr { get; set; } = string.Empty;
    public string? ImageRelativePath { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
