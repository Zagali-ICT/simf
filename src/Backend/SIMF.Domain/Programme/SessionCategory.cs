using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// A session category, held as a lookup rather than an enum so the list can
/// change without a deployment. A <see cref="Session"/> optionally points at
/// one.
///
/// <para>The table ships empty and the team seeds it, because the value list is
/// the client's to decide. Nothing is invented here.</para>
/// </summary>
public sealed class SessionCategory : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Ascending, in both the picker and the admin grid.</summary>
    public int DisplayOrder { get; set; }
}
