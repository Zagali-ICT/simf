using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>A lookup rather than an enum so the list can change without a deployment.
/// The table ships empty: the value list is the client's to decide.</summary>
public sealed class SessionCategory : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
