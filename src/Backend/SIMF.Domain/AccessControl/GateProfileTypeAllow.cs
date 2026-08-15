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

    /// <summary>The permitted profile type. Profile types live in this same
    /// database, so this is a real foreign key with Restrict, declared without a
    /// navigation property; the admin gate service also checks the reference at
    /// write time so a bad id fails validation rather than the database.</summary>
    public Guid ProfileTypeId { get; set; }
}
