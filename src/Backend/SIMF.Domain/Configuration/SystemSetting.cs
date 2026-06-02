namespace SIMF.Domain.Configuration;

/// <summary>P2.4 — D-229 (SIMF-FDS-012 §5.5): one platform/system setting the
/// admin edits without a release — the settings that are NOT content and NOT
/// categories. A simple keyed string value with an optional description; the
/// full list of keys is the client's (open item FDS-012 OI-2), so the table
/// ships EMPTY and the team seeds it via the CRUD — nothing is invented here.
/// Soft-deleted via <see cref="IsActive"/>.</summary>
public sealed class SystemSetting
{
    public Guid Id { get; set; }

    /// <summary>Unique machine key (≤128, e.g. "event.contactEmail").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The current value (≤2048).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional human description of what the setting controls (≤512).</summary>
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
