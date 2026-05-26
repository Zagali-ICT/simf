using Microsoft.EntityFrameworkCore;
using SIMF.Domain.Auditing;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// The application database context — the SIMF business entities. It shares one
/// physical database with <see cref="SimfIdentityDbContext"/> (decision C-1) but
/// keeps its own migration history table.
/// </summary>
public class SimfAppDbContext(DbContextOptions<SimfAppDbContext> options) : DbContext(options)
{
    /// <summary>The operation log — the durable audit trail (SIMF-FDS-001 section 9).</summary>
    public DbSet<OperationLogEntry> OperationLog => Set<OperationLogEntry>();

    /// <summary>D-109: row-audit trail for changes against this DbContext.</summary>
    public DbSet<RowAudit> RowAudits => Set<RowAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SimfAppDbContext).Assembly,
            type => type.Namespace == "SIMF.Infrastructure.Persistence.Configurations.App");
    }
}
