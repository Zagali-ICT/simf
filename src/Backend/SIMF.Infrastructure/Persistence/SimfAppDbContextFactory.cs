using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using SIMF.Common.Options;
using SIMF.Infrastructure.Identity;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>) to create a
/// <see cref="SimfAppDbContext"/> when generating migrations. It is not used at
/// run time. D-157: the connection string is read from
/// <c>SIMF_DESIGN_TIME_APP_CONNECTION</c> (or the legacy
/// <c>SIMF_DESIGN_TIME_CONNECTION</c>), falling back to the dev
/// <c>SIMF_App</c> database on the local SQL Server default instance.
/// </summary>
public sealed class SimfAppDbContextFactory : IDesignTimeDbContextFactory<SimfAppDbContext>
{
    public SimfAppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SIMF_DESIGN_TIME_APP_CONNECTION")
            ?? Environment.GetEnvironmentVariable("SIMF_DESIGN_TIME_CONNECTION")
            ?? "Server=.;Database=SIMF_App;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<SimfAppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory_App"))
            .Options;

        // A design-time encryptor with no key — migration scaffolding inspects the
        // model (the value converter's presence/type) but never encrypts/decrypts.
        var pii = new AesGcmPiiEncryptor(Options.Create(new StorageOptions()));
        return new SimfAppDbContext(options, pii);
    }
}
