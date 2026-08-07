namespace SIMF.Contracts.PublicRelations;

// ---------------------------------------------------------------------------
// News / Media-Centre contracts (Mockup screen 29 / 29b).
// Public payloads are the app / website shape; admin payloads drive the CP
// grid + edit modal. Lives in the existing SIMF.Contracts.PublicRelations
// namespace alongside the Invitation contracts.
// ---------------------------------------------------------------------------

/// <summary>One card in the public News feed (Mockup screen 29). Carries the
/// teaser excerpt shown on the card; the full body is fetched via the detail
/// endpoint when a card is opened.</summary>
public sealed record PublicNewsListItem(
    Guid Id,
    string Title,
    string TitleArabic,
    string? Excerpt,
    string? ExcerptArabic,
    string Category,
    string CategoryArabic,
    string? ImageRelativePath,
    DateTime PublishedAt);

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
    string Title,
    string TitleArabic,
    string Body,
    string BodyArabic,
    string Category,
    string CategoryArabic,
    string? ImageRelativePath,
    DateTime PublishedAt);

/// <summary>One row in the admin News grid (CP). The grid and detail share the
/// summary except the detail adds the long-form body + excerpt + image. Admin
/// reads show every row regardless of <c>IsActive</c> / publish window so
/// editors can manage drafts and reactivate soft-deleted items.</summary>
public sealed record AdminNewsSummary(
    Guid Id,
    string Title,
    string TitleArabic,
    string Category,
    string CategoryArabic,
    DateTime PublishedAt,
    int DisplayOrder,
    bool IsActive,
    // True when an active NewsImage asset exists, so the grid renders the
    // image thumbnail (SimfIdentityCell), else an initials tile.
    bool HasImage,
    DateTime CreatedAt,
    // Carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Optional; the long-form body is required on the entity so
    // BodyArabic is always present, ExcerptArabic is blank when unset.
    string BodyArabic = "",
    string? ExcerptArabic = null);

/// <summary>Full article detail for the CP Details / Edit modal.</summary>
public sealed record AdminNewsDetail(
    Guid Id,
    string Title,
    string TitleArabic,
    string? Excerpt,
    string? ExcerptArabic,
    string Body,
    string BodyArabic,
    string Category,
    string CategoryArabic,
    string? ImageRelativePath,
    DateTime PublishedAt,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Create payload (admin). Mirrors <c>CreateDelegationRequest</c> shape.</summary>
public sealed class CreateNewsRequest
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? ExcerptArabic { get; set; }
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryArabic { get; set; } = string.Empty;
    public string? ImageRelativePath { get; set; }
    public DateTime PublishedAt { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Update payload (admin). Adds <c>IsActive</c> to the create shape.</summary>
public class UpdateNewsRequest
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? ExcerptArabic { get; set; }
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryArabic { get; set; } = string.Empty;
    public string? ImageRelativePath { get; set; }
    public DateTime PublishedAt { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
