using SIMF.Domain.IdentityAccess;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// The join row permitting one profile type through a <see cref="Gate"/>, keyed
/// on the pair.
/// </summary>
public class GateProfileTypeAllow
{
    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>The permitted profile type. There is no navigation and no
    /// database constraint here; the admin gate service checks the reference at
    /// write time instead.</summary>
    public Guid ProfileTypeId { get; set; }
}
