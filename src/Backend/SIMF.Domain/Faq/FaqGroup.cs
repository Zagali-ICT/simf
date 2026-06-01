namespace SIMF.Domain.Faq;

/// <summary>
/// P2.1 (D-211) — a top-level FAQ category (e.g. "Registration",
/// "Venue &amp; Travel"). Groups own an ordered list of <see cref="FaqEntry"/>
/// question/answer pairs. Bilingual, orderable, soft-deletable — mirrors the
/// News module's CRUD shape.
/// </summary>
public sealed class FaqGroup
{
    public Guid Id { get; set; }

    /// <summary>English group name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic group name — paired with <see cref="NameEn"/>.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Sort key within the FAQ list (ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Soft-delete flag — false hides the group (and is treated as
    /// hiding its entries) without losing the rows.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>The group's question/answer entries.</summary>
    public ICollection<FaqEntry> Entries { get; set; } = new List<FaqEntry>();
}
