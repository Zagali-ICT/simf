using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// The Identity and Access database context — users, roles, permissions,
/// refresh tokens and account codes. Built on ASP.NET Core Identity
/// (SIMF-DAT-001 section 5.1, Amendment A.1). It shares one physical database
/// with <see cref="SimfAppDbContext"/> (decision C-1) but keeps its own
/// migration history table.
///
/// <para>R5a — D-090: the EF-tracked user entity is the Infrastructure-owned
/// <see cref="IdentitySimfUser"/> shim, not the Domain <see cref="SimfUser"/>.
/// Table name (<c>AspNetUsers</c>) and column shape are unchanged — only the
/// CLR type EF projects rows into has moved off the Domain. This is the seam
/// R5f uses to strip <see cref="Microsoft.AspNetCore.Identity.IdentityUser{T}"/>
/// out of <see cref="SimfUser"/>'s inheritance chain.</para>
/// </summary>
public class SimfIdentityDbContext(DbContextOptions<SimfIdentityDbContext> options)
    : IdentityDbContext<IdentitySimfUser, IdentitySimfRole, Guid>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AccountCode> AccountCodes => Set<AccountCode>();

    public DbSet<SecondFactorToken> SecondFactorTokens => Set<SecondFactorToken>();

    public DbSet<TotpRecoveryCode> TotpRecoveryCodes => Set<TotpRecoveryCode>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<ProfileType> ProfileTypes => Set<ProfileType>();

    public DbSet<Interest> Interests => Set<Interest>();

    public DbSet<SIMF.Domain.Notifications.Notification> Notifications =>
        Set<SIMF.Domain.Notifications.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SimfIdentityDbContext).Assembly,
            type => type.Namespace == "SIMF.Infrastructure.Persistence.Configurations");
    }
}
