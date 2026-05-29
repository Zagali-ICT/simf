namespace SIMF.Domain.AccessControl;

/// <summary>
/// D-148 — join row linking a <see cref="Gate"/> to a permitted
/// <c>ProfileType</c> (composite PK: (GateId, ProfileTypeId)). The
/// <c>ProfileTypeId</c> is a logical FK — referential integrity is enforced
/// at write time by <c>AdminGateService</c> (cross-context — DAT-001 §5.3.1).
/// </summary>
public class GateProfileTypeAllow
{
    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>The permitted ProfileType. Logical FK — validated by the
    /// service, not by a DB constraint.</summary>
    public Guid ProfileTypeId { get; set; }
}
