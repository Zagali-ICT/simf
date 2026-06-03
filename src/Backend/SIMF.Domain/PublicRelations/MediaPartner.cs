namespace SIMF.Domain.PublicRelations;

/// <summary>
/// D-199 (Mockup page 31 — "شركاء النجاح" / "شركاء وسائل الإعلام") — one media
/// partner shown in the mobile app's media-partner grid. Each card renders a
/// logo (<see cref="LogoRelativePath"/>) and, optionally, links out to the
/// partner's site (<see cref="Url"/>). The public list is ordered by
/// <see cref="DisplayOrder"/> ascending, tie-broken by <see cref="NameAr"/>.
/// </summary>
public sealed class MediaPartner
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic display name — the primary surface on the mobile app
    /// (1–256 chars).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Relative path to the partner's logo asset (e.g.
    /// "media-partners/cnn.png"), resolved against the app's static asset
    /// root. Optional — a card with no logo falls back to the name.
    /// Stored as a relative path (never an absolute URL) so the asset host
    /// can change without rewriting rows. ≤ 512 chars.</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Optional outbound link to the partner's website. ≤ 512 chars.
    /// Null when the partner has no public site to link to.</summary>
    public string? Url { get; set; }

    /// <summary>Sort key on the public list — ascending. Tie-broken by
    /// <see cref="NameAr"/>. (≥ 0.)</summary>
    public int DisplayOrder { get; set; }

    /// <summary>SIMF-FDS-014 (D-254) — optional link to the shared <c>Contact</c>
    /// directory record (logo / name / phones / social / website / location /
    /// country). Null until linked; multiple entities may reference the same
    /// Contact. The public projection prefers the Contact when set.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>Soft-delete flag. Inactive rows never appear on the public
    /// list and are filtered out of every active query.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
