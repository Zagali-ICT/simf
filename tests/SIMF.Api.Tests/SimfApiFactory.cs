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
/// The connection string and other settings are passed as environment
/// variables because <c>AddInfrastructure</c> reads configuration eagerly,
/// before the test host's configuration callbacks would run. Test parallelism
/// is disabled (see <c>AssemblyInfo.cs</c>) so the process-wide variables and
/// the shared clock are safe. The rate limit is set high so the normal suite is
/// not throttled; <see cref="RateLimitedApiFactory"/> lowers it for the
/// rate-limit tests.
/// </remarks>
public class SimfApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"SIMF_Test_{Guid.NewGuid():N}";

    public FakeEmailSender Email { get; } = new();

    // Started near real time so a test-issued JWT — whose lifetime the bearer
    // middleware validates against the real system clock — is not seen as
    // expired. Tests advance this clock explicitly when they need to.
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>
    /// Per-test-run temp directory the FilesystemAvatarStorage writes into,
    /// cleaned up on <see cref="Dispose(bool)"/>.
    /// </summary>
    public string AvatarStorageDirectory { get; } =
        Path.Combine(Path.GetTempPath(), $"simf-avatars-{Guid.NewGuid():N}");

    /// <summary>Temp dir for encrypted visitor ID-image files (D-046 b).</summary>
    public string VisitorIdStorageDirectory { get; } =
        Path.Combine(Path.GetTempPath(), $"simf-visitor-ids-{Guid.NewGuid():N}");

    public SimfApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SimfDb",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("SuperAdmin__Email", "superadmin@simf.test");
        Environment.SetEnvironmentVariable("SuperAdmin__TempPassword", "ChangeMe!Test1");
        Environment.SetEnvironmentVariable("SuperAdmin__TotpSecret", "JBSWY3DPEHPK3PXP");
        Environment.SetEnvironmentVariable("RateLimit__PermitLimit", "100000");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", "ytlV1+ke14Pw900IRtH8zT4uIKBeaqjcj6aFfiLozS5jKgSs");
        Environment.SetEnvironmentVariable("Storage__AvatarBase", AvatarStorageDirectory);
        // A fixed base64-encoded 32-byte AES key for the test environment so
        // the encrypted ID-image round-trip is deterministic across runs.
        Environment.SetEnvironmentVariable("Storage__VisitorIdBase", VisitorIdStorageDirectory);
        Environment.SetEnvironmentVariable(
            "Storage__VisitorIdEncryptionKey",
            "VnY3R0V2YnFwT0ZQUE1XdjJxQjJlbzVwUFp4MnNYbWY=");
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

    /// <summary>Applies the migrations to the test database. Call once per test class.</summary>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<SimfIdentityDbContext>().Database.Migrate();
        services.GetRequiredService<SimfAppDbContext>().Database.Migrate();
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

            try
            {
                if (Directory.Exists(AvatarStorageDirectory))
                {
                    Directory.Delete(AvatarStorageDirectory, recursive: true);
                }
            }
            catch (Exception)
            {
                // Best-effort temp-directory cleanup; ditto.
            }
        }

        base.Dispose(disposing);
    }
}
