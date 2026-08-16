using SIMF.Domain.IdentityAccess;

namespace SIMF.Domain.AccessControl;

public class GateProfileTypeAllow
{
    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>Also checked by the admin gate service at write time, so a bad id fails validation rather than the database.</summary>
    public Guid ProfileTypeId { get; set; }
}
