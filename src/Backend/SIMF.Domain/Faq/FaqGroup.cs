using SIMF.Domain.Common;

namespace SIMF.Domain.Faq;

/// <summary>
/// P2.1 (D-211) — a top-level FAQ category (e.g. "Registration",
/// "Venue &amp; Travel"). Groups own an ordered list of <see cref="FaqEntry"/>
/// question/answer pairs. Bilingual, orderable, soft-deletable — mirrors the
/// News module's CRUD shape.
/// </summary>
public sealed class FaqGroup:BaseAuditEntity
{
    
    /// <summary>English group name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic group name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Sort key within the FAQ list (ascending).</summary>
    public int DisplayOrder { get; set; }

   
     

    /// <summary>The group's question/answer entries.</summary>
    public ICollection<FaqEntry> Entries { get; set; } = new List<FaqEntry>();
}
