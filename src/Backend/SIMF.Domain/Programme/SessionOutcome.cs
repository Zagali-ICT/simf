using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One bulleted key outcome of a <see cref="Session"/>, a line in the checklist
/// on the public session page.
/// </summary>
public sealed class SessionOutcome : BaseAuditEntity
{
    /// <summary>A real foreign key, cascading: outcomes belong to the session and
    /// go with it.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Text"/>.</summary>
    public string TextArabic { get; set; } = string.Empty;

    /// <summary>Ascending, within the session's outcomes.</summary>
    public int DisplayOrder { get; set; }
}
