using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>) to create a
/// <see cref="SimfIdentityDbContext"/> when generating migrations. It is not
/// used at run time — the running application registers the context through
/// dependency injection. The connection string is read from the
/// <c>SIMF_DESIGN_TIME_CONNECTION</c> environment variable, falling back to a
/// local SQL Server LocalDB instance.
/// </summary>
public sealed class SimfIdentityDbContextFactory : IDesignTimeDbContextFactory<SimfIdentityDbContext>
{
    public SimfIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SIMF_DESIGN_TIME_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SIMF;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<SimfIdentityDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .Options;

        return new SimfIdentityDbContext(options);
    }
}
