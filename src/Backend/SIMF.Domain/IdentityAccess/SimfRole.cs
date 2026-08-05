using Microsoft.AspNetCore.Identity;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A SIMF role. Extends ASP.NET Core Identity's <see cref="IdentityRole{TKey}"/>.
/// Roles are dynamic: an administrator creates them and assigns permissions.
/// </summary>
public class SimfRole : IdentityRole<Guid>
{
    /// <summary>
    /// True for a built-in role that ships with the system and cannot be
    /// deleted; false for an administrator-created role.
    /// </summary>
    public bool IsBaseline { get; set; }
}
