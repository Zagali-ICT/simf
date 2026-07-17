using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>One bulleted "key outcome" of a <see cref="Session"/> — a line in
/// the "أبرز المخرجات" checklist on the public session-detail page (Figma
/// 5991-85840). Bilingual text, ordered by <see cref="DisplayOrder"/>;
/// soft-deleted via <see cref="BaseAuditEntity.IsActive"/> and cascade-deleted
/// with its session. Additive under the D-219 freeze-lift (App context only).</summary>
public sealed class SessionOutcome : BaseAuditEntity
{
    /// <summary>The owning session. Real FK (same DbContext), cascade — outcomes
    /// belong to the session and go with it.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>English outcome line (≤512).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Arabic outcome line — paired with <see cref="Text"/> (≤512).</summary>
    public string TextArabic { get; set; } = string.Empty;

    /// <summary>Sort key within the session's outcomes (≥ 0; ascending).</summary>
    public int DisplayOrder { get; set; }
}
