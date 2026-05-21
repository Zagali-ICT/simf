using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>) to create a
/// <see cref="SimfAppDbContext"/> when generating migrations. It is not used at
/// run time. See <see cref="SimfIdentityDbContextFactory"/> for the connection
/// string source.
/// </summary>
public sealed class SimfAppDbContextFactory : IDesignTimeDbContextFactory<SimfAppDbContext>
{
    public SimfAppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SIMF_DESIGN_TIME_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SIMF;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<SimfAppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory_App"))
            .Options;

        return new SimfAppDbContext(options);
    }
}
