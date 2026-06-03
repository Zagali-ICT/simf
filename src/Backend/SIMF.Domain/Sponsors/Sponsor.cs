namespace SIMF.Domain.Sponsors;

/// <summary>
/// D-199 (Mockup page 23, "الرعاة" / Sponsors) — one event sponsor shown on the
/// public sponsors screen and managed from the Control Panel. Sponsors are
/// grouped on the public surface by <see cref="Tier"/> (Platinum first) and,
/// within a tier, ordered by <see cref="DisplayOrder"/> ascending then
/// <see cref="NameArabic"/>. Soft-deleted via <see cref="IsActive"/> /
/// <see cref="Deactivate"/> — the public list filters <c>IsActive == true</c>.
/// </summary>
public sealed class Sponsor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars) — the primary surface on the
    /// mobile app and public website.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Sponsorship tier. Drives both the grouping heading and the
    /// top-level ordering on the public screen.</summary>
    public SponsorTier Tier { get; set; } = SponsorTier.Bronze;

    /// <summary>Optional path (relative to the configured media root) of the
    /// sponsor's logo image. Null when no logo has been uploaded yet — the
    /// public card then falls back to the name.</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Optional absolute external URL for the sponsor's website. Null
    /// when the sponsor has no public link.</summary>
    public string? Url { get; set; }

    /// <summary>Sort key within a tier — ascending. Tie-broken by
    /// <see cref="NameArabic"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>SIMF-FDS-014 (D-260) — optional link to the shared <c>Contact</c>
    /// directory record (logo / name / phones / social / website / location /
    /// country). Null until linked; multiple entities may reference the same
    /// Contact. The public projection prefers the Contact when set.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>Soft-delete flag. List endpoints filter on this.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Soft-delete: marks the sponsor inactive so it drops out of the
    /// public list and the default admin grid. Idempotent.</summary>
    public void Deactivate() => IsActive = false;
}
