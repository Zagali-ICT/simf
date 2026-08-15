using SIMF.Domain.Common;

namespace SIMF.Domain.Configuration;

/// <summary>One admin-editable platform setting, for values that are neither content nor a lookup. The table ships empty: the list of keys is the client's to decide.</summary>
public sealed class SystemSetting : BaseAuditEntity
{
    /// <summary>A machine key, such as "event.contactEmail".</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    /// <summary>What the setting controls, for whoever edits it next.</summary>
    public string? Description { get; set; }
}
