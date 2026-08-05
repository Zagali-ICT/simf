using SIMF.Domain.Common;

namespace SIMF.Domain.Profiles;

/// <summary>
/// An interest or focus area a visitor picks while completing their profile,
/// between one and ten of them. Admin-managed rather than hardcoded.
/// Deactivating one stops it appearing in the picker without disturbing the
/// users who already chose it.
///
/// <para>The many-to-many with <see cref="UserProfile"/> uses EF's generated
/// join table, whose two foreign keys both cascade, so removing either side
/// cleans up the join row.</para>
/// </summary>
public sealed class UserInterest : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Ascending, tie-broken by <see cref="Name"/>.</summary>
    public int DisplayOrder { get; set; }
}
