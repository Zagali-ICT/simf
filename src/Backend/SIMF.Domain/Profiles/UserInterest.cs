using SIMF.Domain.Common;

namespace SIMF.Domain.Profiles;

/// <summary>An admin-managed focus area a visitor picks while completing their profile;
/// deactivating one hides it from the picker without disturbing users who already chose it.</summary>
public sealed class UserInterest : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
