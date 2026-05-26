using Microsoft.AspNetCore.Identity;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// R5g — D-093: the Infrastructure-owned persistence shape for the SIMF
/// role row. Mirrors the pre-R5g <c>SimfRole</c> shape one-for-one and is
/// the type EF maps to <c>AspNetRoles</c>. No Domain consumer references
/// roles directly (Application talks about roles by string name via
/// <c>IUserAccountRepository.GetRolesAsync</c>); the pre-R5g
/// <c>SimfRole</c> Domain class is removed entirely.
///
/// <para><c>public sealed</c> so the public
/// <c>SimfIdentityDbContext</c>'s base type (parameterised on this role)
/// has matching accessibility. The test project reaches the class via
/// <c>InternalsVisibleTo("SIMF.Api.Tests")</c> on Infrastructure for the
/// fixtures that seed roles directly through
/// <c>RoleManager&lt;IdentitySimfRole&gt;</c>.</para>
/// </summary>
public sealed class IdentitySimfRole : IdentityRole<Guid>
{
    /// <summary>
    /// True for a built-in role that ships with the system and cannot be
    /// deleted; false for an administrator-created role.
    /// </summary>
    public bool IsBaseline { get; set; }
}
