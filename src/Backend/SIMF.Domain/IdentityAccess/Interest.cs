namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A user-pickable interest / discipline / focus area (P9 — D-050;
/// الاهتمامات). Bilingual lookup the visitor self-selects from when
/// completing their profile (1-10 picks per profile). Admin-managed
/// CRUD via the CP — no hardcoded list. Soft-deleted via
/// <see cref="IsActive"/> = false so a row stops appearing in the
/// picker without breaking any existing user that already chose it.
///
/// <para>The M-to-M to <see cref="UserProfile"/> is realised by EF's
/// auto-generated join table <c>UserProfileInterests</c> (composite PK,
/// both FKs cascade so deleting either side cleans up the join row).</para>
/// </summary>
public sealed class Interest
{
    public Guid Id { get; set; }

    /// <summary>English display name — for example "Maritime Security".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Sort key in the visitor picker (ascending). Tie-broken by
    /// <see cref="Name"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Soft-delete flag — false hides the row from the visitor
    /// picker but keeps existing user assignments intact.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
