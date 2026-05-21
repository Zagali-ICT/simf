using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SIMF.Application.Email;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Tests;

/// <summary>
/// Hosts the API for integration tests against a throwaway SQL Server LocalDB
/// database (the real provider), with the email sender replaced by a
/// <see cref="FakeEmailSender"/> and the clock by a controllable
/// <see cref="FakeTimeProvider"/>.
/// </summary>
/// <remarks>
/// The connection string and the super-admin settings are passed as environment
/// variables because <c>AddInfrastructure</c> reads configuration eagerly,
/// before the test host's configuration callbacks would run. Test parallelism
/// is disabled (see <c>AssemblyInfo.cs</c>) so the process-wide variables and
/// the shared clock are safe.
/// </remarks>
public sealed class SimfApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"SIMF_Test_{Guid.NewGuid():N}";

    public FakeEmailSender Email { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public SimfApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SimfDb",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("SuperAdmin__Email", "superadmin@simf.test");
        Environment.SetEnvironmentVariable("SuperAdmin__TempPassword", "ChangeMe!Test1");
        Environment.SetEnvironmentVariable("SuperAdmin__TotpSecret", "JBSWY3DPEHPK3PXP");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Creates the test database schema. Call once per test class.</summary>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>()
            .Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>()
                    .Database.EnsureDeleted();
            }
            catch (Exception)
            {
                // Best-effort cleanup of the throwaway test database; a failure
                // here must not fail the test run.
            }
        }

        base.Dispose(disposing);
    }
}
