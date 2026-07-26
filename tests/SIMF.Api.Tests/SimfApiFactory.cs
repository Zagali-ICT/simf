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
    // D-157 — two physically separate test databases (one per context).
    private readonly string _identityDatabaseName = $"SIMF_Test_Identity_{Guid.NewGuid():N}";
    private readonly string _appDatabaseName = $"SIMF_Test_App_{Guid.NewGuid():N}";

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

    /// <summary>Temp dir for encrypted user ID-document files (decisions
    /// D-046 b, P8 — D-049; renamed from <c>VisitorIdStorageDirectory</c>).</summary>
    public string UserIdDocumentStorageDirectory { get; } =
        Path.Combine(Path.GetTempPath(), $"simf-user-id-documents-{Guid.NewGuid():N}");

    /// <summary>D-568 — per-test-run root for the centralized file store
    /// (<c>FileStorage:RootPath</c>), cleaned up on <see cref="Dispose(bool)"/>.</summary>
    public string FileStorageDirectory { get; } =
        Path.Combine(Path.GetTempPath(), $"simf-files-{Guid.NewGuid():N}");

    /// <summary>
    /// DEF-SEC-001 — the shared password the demo @simf.local accounts are
    /// seeded with (<c>Seed:DemoPassword</c>). D-585 seeds those accounts in
    /// every environment, so the value must NOT be committed: it is read from
    /// <c>SIMF_TEST_DEMO_PASSWORD</c> when a developer or CI supplies one, and
    /// otherwise generated once per test process so the suite still runs
    /// offline with no configuration. Generated once (static) so every factory
    /// in a run agrees. No test asserts the literal — the demo accounts are
    /// only checked for existence, role and profile — so a per-run value is
    /// safe. The shape satisfies the Identity password policy (upper, lower,
    /// digit, non-alphanumeric).
    /// </summary>
    private static readonly string DemoSeedPassword =
        Environment.GetEnvironmentVariable("SIMF_TEST_DEMO_PASSWORD") is { Length: > 0 } supplied
            ? supplied
            : $"TestOnly!{Guid.NewGuid():N}Aa1";

    public SimfApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SimfIdentityDb",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_identityDatabaseName};" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SimfAppDb",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_appDatabaseName};" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("SuperAdmin__Email", "superadmin@simf.test");
        Environment.SetEnvironmentVariable("SuperAdmin__TempPassword", "ChangeMe!Test1");
        Environment.SetEnvironmentVariable("SuperAdmin__TotpSecret", "JBSWY3DPEHPK3PXP");
        Environment.SetEnvironmentVariable("RateLimit__PermitLimit", "100000");
        // H7 — D-062: the new per-email partition (auth-email policy)
        // would otherwise cap test scenarios that intentionally retry
        // wrong credentials against one email. Permissive default here;
        // EmailRateLimitedApiFactory tightens this for the email-cap test.
        Environment.SetEnvironmentVariable("RateLimit__EmailPermitLimit", "100000");
        // H29 — D-088: the new global limiter (per-IP, applied to every
        // request) would otherwise trip in long-running test classes
        // that hit hundreds of endpoints in series. Permissive default
        // here; dedicated rate-limit-test factories can tighten as
        // needed in future.
        Environment.SetEnvironmentVariable("RateLimit__GlobalPermitLimit", "1000000");
        // A7-13 — password expiry OFF by default for the general suite. Reset here
        // (these env vars are process-wide) so a prior PasswordExpiryApiFactory
        // class cannot leak MaxAgeDays=30 into later classes — which would expire
        // any user created with a default CreatedAt and break their sign-in.
        Environment.SetEnvironmentVariable("IdentityLifecycle__PasswordMaxAgeDays", "0");
        // A7-20 — password history OFF by default for the general suite (reset the
        // process-wide var so a prior PasswordHistoryApiFactory cannot leak its count).
        Environment.SetEnvironmentVariable("IdentityLifecycle__PasswordHistoryCount", "0");
        // A1-19 — dormant-account auto-disable OFF by default (reset the process-wide
        // var so a prior DormantAccountApiFactory cannot leak its threshold).
        Environment.SetEnvironmentVariable("IdentityLifecycle__DormantAccountDisableDays", "0");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", "ytlV1+ke14Pw900IRtH8zT4uIKBeaqjcj6aFfiLozS5jKgSs");
        Environment.SetEnvironmentVariable("Storage__AvatarBase", AvatarStorageDirectory);
        // A fixed base64-encoded 32-byte AES key for the test environment so
        // the encrypted ID-image round-trip is deterministic across runs.
        // P8 renamed the config keys off Storage__VisitorId* to
        // Storage__UserIdDocument*.
        Environment.SetEnvironmentVariable("Storage__UserIdDocumentBase", UserIdDocumentStorageDirectory);
        Environment.SetEnvironmentVariable(
            "Storage__UserIdDocumentEncryptionKey",
            "VnY3R0V2YnFwT0ZQUE1XdjJxQjJlbzVwUFp4MnNYbWY=");
        // D-568 — the centralized file store writes under a throwaway temp root and
        // encrypts its Confidential/Secret services with a fixed test KEK so the
        // envelope round-trip is deterministic across runs (the real KEK is supplied
        // through the environment in production, never committed).
        Environment.SetEnvironmentVariable("FileStorage__RootPath", FileStorageDirectory);
        Environment.SetEnvironmentVariable(
            "FileStorage__EncryptionKey", "U0lNRnRlc3RGaWxlU3RvcmFnZUtFSzAxMjM0NTY3ODk=");
        // C7 — D-371: the human-face gate is OFF for the general suite (the
        // fixture images are synthetic 1x1 PNGs that carry no face);
        // UserProfileFaceGateTests re-enables it via FaceGateApiFactory to
        // exercise the real offline ONNX model.
        Environment.SetEnvironmentVariable("FaceDetection__Enabled", "false");
        // #7a — the biometric-enrol emailed-OTP step-up is OFF for the general
        // suite so the device-key ceremony tests register without a code;
        // DeviceKeyStepUpTests re-enables it via BiometricStepUpApiFactory to
        // exercise the real gate.
        Environment.SetEnvironmentVariable("DeviceKey__RequireStepUpForEnrol", "false");
        // D-717 (item 7, GAP-3) — a public Website base URL so the speaker
        // action-link mint builds real URLs (and so MeetingActionTokenTests can
        // extract the token secret from the returned link).
        Environment.SetEnvironmentVariable(
            "MeetingLinks__PublicWebBaseUrl", "https://test.simf.local");
        // Round-1 held item #1 — the demo @simf.local accounts (D-585) now seed
        // ONLY in Development or behind Seed:EnableDemoAccounts (default false),
        // and DemoSeedOptions.DemoPassword has no hardcoded default. The general
        // suite (BadgeSignInTests, WalkInRegistrationTests, AdminCreateUserTests,
        // IdentitySeederTests, …) relies on those accounts, and the host runs as
        // "Testing" (not Development), so opt IN explicitly and supply the
        // demo password. Reset here (process-wide vars) so a prior
        // DemoAccountsDisabledApiFactory cannot leak EnableDemoAccounts=false
        // into later classes. DEF-SEC-001 — the password itself is never
        // committed; see DemoSeedPassword above.
        Environment.SetEnvironmentVariable("Seed__EnableDemoAccounts", "true");
        Environment.SetEnvironmentVariable("Seed__DemoPassword", DemoSeedPassword);
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
        // D-174 — the production Program skips the seeder under the
        // "Testing" environment so xunit doesn't pay the cost on every
        // class. Phase G11 / page 39 ships seed content blocks that
        // tests verify, so the test fixture has to invoke the seeder
        // explicitly. Idempotent.
        services.GetRequiredService<SIMF.Infrastructure.Identity.IdentitySeeder>()
            .SeedAsync().GetAwaiter().GetResult();
        // The built-in rating types (App + Session) are seeded at runtime (not via
        // migration InsertData), so the test fixture invokes the seeder too.
        services.GetRequiredService<SIMF.Infrastructure.Feedback.RatingSeeder>()
            .SeedAsync().GetAwaiter().GetResult();
        // Regions are required reference data the app GET /app/regions depends on,
        // seeded at runtime (skipped under Testing), so the fixture invokes it too
        // — mirrors Program.cs (D-547). Idempotent.
        services.GetRequiredService<SIMF.Infrastructure.Regions.RegionSeeder>()
            .SeedAsync().GetAwaiter().GetResult();
        // D-747 — the 2026 event content (speakers, programme, news, sponsors,
        // media partners, archive, org about) moved out of the C# seeders into
        // the by-hand SQL lane (docs/migrations/2026/*.sql, owner rule D-718).
        // Apply the roster files here so a test DB still carries that content —
        // mirrors Program.cs's Development run. SeedGaps (booths/delegations/
        // FAQ/venue) is deliberately excluded to keep the profile-count baseline.
        // Idempotent; missing files (before their slice lands) are skipped.
        services.GetRequiredService<SIMF.Infrastructure.Seeding.SqlContentSeeder>()
            .RunAsync(SIMF.Infrastructure.Seeding.SqlContentSeeder.RosterFiles)
            .GetAwaiter().GetResult();
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
                scope.ServiceProvider.GetRequiredService<SimfAppDbContext>()
                    .Database.EnsureDeleted();
            }
            catch (Exception)
            {
                // Best-effort cleanup of the throwaway test databases; a
                // failure here must not fail the test run.
            }

            try
            {
                if (Directory.Exists(AvatarStorageDirectory))
                {
                    Directory.Delete(AvatarStorageDirectory, recursive: true);
                }
                if (Directory.Exists(FileStorageDirectory))
                {
                    Directory.Delete(FileStorageDirectory, recursive: true);
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
