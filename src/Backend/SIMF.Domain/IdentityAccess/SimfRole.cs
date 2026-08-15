using Microsoft.AspNetCore.Identity;

namespace SIMF.Domain.IdentityAccess;

public class SimfRole : IdentityRole<Guid>
{
    /// <summary>Seeded role: cannot be renamed, deleted or re-permissioned (409).</summary>
    public bool IsBaseline { get; set; }
}
