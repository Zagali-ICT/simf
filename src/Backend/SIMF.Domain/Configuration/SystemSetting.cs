using SIMF.Domain.Common;

namespace SIMF.Domain.Configuration;

/// <summary>
/// One platform setting an admin can edit without a release — a keyed string
/// value with an optional description, for the settings that are neither content
/// nor a lookup.
///
/// <para>The table ships empty and the team seeds it through the ordinary CRUD,
/// because the list of keys is the client's to decide. Nothing is invented
/// here.</para>
/// </summary>
public sealed class SystemSetting : BaseAuditEntity
{
    /// <summary>A unique machine key, such as "event.contactEmail".</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    /// <summary>What the setting controls, in prose, for whoever edits it
    /// next.</summary>
    public string? Description { get; set; }
}
