using SIMF.Domain.Common;

namespace SIMF.Domain.Faq;

/// <summary>
/// A top-level FAQ category, such as "Registration" or "Venue and Travel",
/// owning an ordered list of <see cref="FaqEntry"/> pairs.
/// </summary>
public sealed class FaqGroup : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Ascending, within the FAQ list.</summary>
    public int DisplayOrder { get; set; }

    public ICollection<FaqEntry> Entries { get; set; } = new List<FaqEntry>();
}
