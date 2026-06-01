namespace SIMF.Domain.Programme;

/// <summary>
/// B9b — D-226 (FDS-004 §5.4 + §7): the session category — a DYNAMIC lookup
/// ("a dynamic Category, for example a main session"). One row per category;
/// a <see cref="Session"/> optionally points at one via
/// <c>Session.CategoryId</c>. Bilingual, ordered, soft-deleted via
/// <see cref="IsActive"/>. The value list is the client's to confirm (open
/// item OI-2) — the table ships empty and the team seeds it, exactly like the
/// Organisation lookup; nothing is invented here.
/// </summary>
public sealed class SessionCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>English category name (1–128 chars).</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic category name (1–128 chars).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Sort order in the picker / admin grid (ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Soft-delete flag. List/picker reads filter on this.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Soft-delete transition (CLAUDE.md §7).</summary>
    public void Deactivate() => IsActive = false;
}
