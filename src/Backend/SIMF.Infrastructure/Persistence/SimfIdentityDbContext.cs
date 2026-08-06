using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// The Identity and Access database context — users, roles, permissions,
/// refresh tokens and account codes. Built on ASP.NET Core Identity
/// (SIMF-DAT-001 section 5.1). It lives in its own **physically separate**
/// database (<c>SIMF_Identity</c>), distinct from <see cref="SimfAppDbContext"/>'s
/// <c>SIMF_App</c> database (D-157, superseding the earlier one-shared-DB design
/// C-1). No cross-database relation/FK; App-side user references are bare
/// <c>Guid</c>s resolved on read.
/// </summary>
public class SimfIdentityDbContext(DbContextOptions<SimfIdentityDbContext> options)
    : IdentityDbContext<SimfUser, SimfRole, Guid>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AccountCode> AccountCodes => Set<AccountCode>();

    public DbSet<SecondFactorToken> SecondFactorTokens => Set<SecondFactorToken>();

    public DbSet<TotpRecoveryCode> TotpRecoveryCodes => Set<TotpRecoveryCode>();

    /// <summary>D-172 (gap doc G10, PDF §2.5) — biometric (Face ID)
    /// sign-in device keys.</summary>
    public DbSet<DeviceKey> DeviceKeys => Set<DeviceKey>();

    // UserProfile, ProfileType, Interest moved to SimfAppDbContext.

    public DbSet<SIMF.Domain.Notifications.Notification> Notifications =>
        Set<SIMF.Domain.Notifications.Notification>();

    /// <summary>Row-audit trail for changes against this DbContext.</summary>
    public DbSet<RowAudit> RowAudits => Set<RowAudit>();

    /// <summary>A7-20 (NCA) — retired password hashes for reuse prevention.</summary>
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SimfIdentityDbContext).Assembly,
            type => type.Namespace == "SIMF.Infrastructure.Persistence.Configurations");
    }
}
