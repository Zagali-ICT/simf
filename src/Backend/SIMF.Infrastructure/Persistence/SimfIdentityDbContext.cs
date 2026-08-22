using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// The Identity and Access database context — users, roles, permissions,
/// refresh tokens and account codes. Built on ASP.NET Core Identity.
/// It lives in its own **physically separate**
/// database (<c>SIMF_Identity</c>), distinct from <see cref="SimfAppDbContext"/>'s
/// <c>SIMF_App</c> database, superseding the earlier one-shared-DB
/// design. No cross-database relation/FK; App-side user references are bare
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

    /// <summary>Biometric (Face ID)
    /// sign-in device keys.</summary>
    public DbSet<DeviceKey> DeviceKeys => Set<DeviceKey>();

    // UserProfile, ProfileType, Interest moved to SimfAppDbContext.

    public DbSet<SIMF.Domain.Notifications.Notification> Notifications =>
        Set<SIMF.Domain.Notifications.Notification>();

    /// <summary>Row-audit trail for changes against this DbContext.</summary>
    public DbSet<RowAudit> RowAudits => Set<RowAudit>();

    /// <summary>A7-20 (NCA) — retired password hashes for reuse prevention.</summary>
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();


    /// <summary>Case- and accent-insensitive Arabic collation, applied to every
    /// <c>*Arabic</c> string column.
    ///
    /// <para>What it actually folds, measured against the engine rather than
    /// assumed: the alef maksura and the yeh become one letter, so
    /// <c>مصطفى</c> and <c>مصطفي</c> match for search,
    /// for equality and inside the unique indexes. That is one of the two
    /// commonest Arabic spelling splits and it was silently costing rows before.</para>
    ///
    /// <para>What it does NOT fold, which is worth knowing before promising it to
    /// anyone: a PRECOMPOSED alef-hamza is not equal to a bare alef, so
    /// <c>أحمد</c> is still not found by a search for
    /// <c>احمد</c>. Accent-insensitivity discards a secondary weight,
    /// and only the decomposed sequence U+0627 U+0654 carries one; the
    /// precomposed U+0623 has a primary weight of its own and is simply a
    /// different letter to SQL Server. Every Arabic keyboard emits the
    /// precomposed form. Folding those would need a normalised shadow of the text
    /// written on the way in and searched instead of the display column, which is
    /// a schema change - normalising only the needle cannot change the stored
    /// haystack.</para></summary>
    private const string ArabicCollation = "Arabic_CI_AI";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The seven Identity tables carry SIMF names rather than the framework's
        // AspNet* defaults, so an operator reading this database sees one naming
        // convention across all of it rather than two. They sit in one block, and
        // not spread across the configuration classes, because they are one
        // decision and a reader has to see at a glance that the set is complete:
        // five of the seven are stock closed generics with nothing else to
        // configure, so splitting them would mean five files each holding a single
        // line. The generic arguments must stay exactly the ones
        // IdentityDbContext<SimfUser, SimfRole, Guid> maps -- a mismatched argument
        // silently ADDS a second entity type instead of renaming the mapped one,
        // and the model still builds.
        // Nothing here touches the "[AspNetUserStore]" LoginProvider value that
        // Identity writes into the tokens table: that is a row value, not a table
        // name, and changing it would break TOTP for every enrolled account.
        // Arabic search has been accent-SENSITIVE since the beginning, because no
        // collation was ever set and both databases therefore run the SQL Server
        // instance default (a *_CI_AS). Under it "مصطفى" does not match "مصطفي" - the
        // alef maksura and the yeh are different characters, and the same name is
        // routinely typed both ways by the same person. Every admin grid search box
        // and every public search over a bilingual column was quietly missing rows,
        // and there is NO fix in C# - normalising the needle cannot change how the
        // server compares it to the stored haystack.
        //
        // Measured, not assumed, and narrower than it first looks. Arabic_CI_AI
        // folds maksura/yeh; it does NOT fold a PRECOMPOSED alef-hamza onto a bare
        // alef, so "أحمد" is still not found by searching "احمد". Accent-
        // insensitivity discards a secondary weight and only the decomposed
        // sequence U+0627 U+0654 has one, while the precomposed U+0623 carries a
        // primary weight of its own - and a precomposed alef is what every Arabic
        // keyboard produces. Closing that second gap needs a normalised shadow
        // column written on the way in, not a collation.
        //
        // Accent-INSENSITIVE collation is the fix, applied per column rather than
        // per database so it lands on exactly the text it is meant for: every
        // string property whose name ends "Arabic". The bilingual convention across
        // this model is a pair (Name, NameArabic), so the suffix IS the set, and
        // keying on it means a column added later inherits the collation without
        // anyone remembering to ask for it.
        //
        // What this changes besides search: equality does too, so a duplicate probe
        // over an Arabic name now treats the two spellings as the same name. That is
        // the desired reading of "already exists" rather than a side effect. No unique index on this
        // context covers an Arabic column; the only Arabic text here is the
        // notification title and body.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string)
                    && property.Name.EndsWith("Arabic", StringComparison.Ordinal))
                {
                    property.SetCollation(ArabicCollation);
                }
            }
        }

        modelBuilder.Entity<SimfUser>().ToTable("Users");
        modelBuilder.Entity<SimfRole>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SimfIdentityDbContext).Assembly,
            type => type.Namespace == "SIMF.Infrastructure.Persistence.Configurations");
    }
}
