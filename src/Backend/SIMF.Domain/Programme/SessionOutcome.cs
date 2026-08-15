using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>One bulleted key outcome of a <see cref="Session"/>, a line in the
/// checklist on the public session page.</summary>
public sealed class SessionOutcome : BaseAuditEntity
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public string Text { get; set; } = string.Empty;

    public string TextArabic { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
