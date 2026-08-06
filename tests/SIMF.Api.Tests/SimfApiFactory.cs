using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SIMF.Application.Email;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

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
    //
    // D-848 — the offset is named rather than inferred. SimfClock.Now is a
    // Saudi wall-clock reading with Kind = Unspecified, and the implicit
    // DateTime → DateTimeOffset conversion resolves that against
    // TimeZoneInfo.Local — so the fake clock was seeded at
    // realUtc + (3h − hostOffset), and "near real time" held only on a
    // UTC+03:00 machine. Anywhere else every authenticated test minted a
    // token the bearer middleware then rejected as not-yet-valid or expired.
    // Identical on a +03:00 host; correct on all the others.
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(SimfClock.Now, SimfClock.Offset));

    // AvatarStorageDirectory and UserIdDocumentStorageDirectory lived here until
    // 2026-08-06. They were the per-asset temp roots for FilesystemAvatarStorage
    // and its ID-document sibling, both of which D-568 deleted in favour of the
    // unified StoredFile store; no type of either name survives, nothing wrote to
    // the directories, and no test read them. FileStorageDirectory below is the
    // one root the fixture still needs.

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
        // The super-admin seed settings, pinned so the suite is hermetic.
        //
        // Each is set TWICE, unprefixed and `SIMF_`-prefixed, because Program.cs
        // adds `AddEnvironmentVariables("SIMF_")` AFTER the host's default
        // unprefixed provider — so for any key that has a `SIMF_` form on the
        // machine, that form wins and an unprefixed pin here is silently ignored.
        // A developer box is documented to export
        // `SIMF_SuperAdmin__PasswordChangeRequired=false` (so the seeded CP login
        // is not forced to rotate), which overrode the `SuperAdminOptions` default
        // of true and failed `IdentitySeederTests.SeedAsync_creates_the_super_admin`
        // — a test whose result depended on whose machine ran it. Pinning both
        // forms closes that for every one of these settings, not just the one that
        // happened to be set here.
        foreach (var (key, value) in new[]
        {
            ("SuperAdmin__Email", "superadmin@simf.test"),
            ("SuperAdmin__TempPassword", "ChangeMe!Test1"),
            ("SuperAdmin__TotpSecret", "JBSWY3DPEHPK3PXP"),
            ("SuperAdmin__PasswordChangeRequired", "true"),
        })
        {
            Environment.SetEnvironmentVariable(key, value);
            Environment.SetEnvironmentVariable("SIMF_" + key, value);
        }
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
        // #2 (Q1, 2026-07-31) — mandatory Control-Panel 2FA enrolment ON, which is
        // the production default (IdentityLifecycleOptions initialises it true and
        // appsettings.json states it). This used to be pinned OFF, because the ~150
        // admin fixtures in this assembly created their user straight through
        // UserManager and then read `Tokens.AccessToken` off a Cp-audience password
        // sign-in — a flow the gate correctly answers with an enrolment challenge
        // and no token — which meant those tests all exercised the pre-fix path.
        // They now go through AuthFlow.SignInControlPanelAsync, which enrols the
        // fixture admin and completes a real TOTP step, so the whole suite runs the
        // shipping posture. Pinned explicitly (a process-wide var) so a future
        // opt-out factory cannot leak "false" into a later test class.
        Environment.SetEnvironmentVariable(
            "IdentityLifecycle__RequireControlPanelTwoFactorEnrolment", "true");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", "ytlV1+ke14Pw900IRtH8zT4uIKBeaqjcj6aFfiLozS5jKgSs");
        // Storage__AvatarBase and Storage__UserIdDocumentBase were set here until
        // 2026-08-06. Both config keys were removed when the unified StoredFile
        // store replaced the bespoke per-asset stores, so setting them bound to
        // nothing; the file store's own root is what the fixture configures below.
        //
        // A fixed base64-encoded 32-byte AES key for the test environment so
        // the encrypted ID-image round-trip is deterministic across runs.
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
        // BUG-023 — the demo OPERATIONAL configuration (gates + operator
        // assignment, per-session moderator grants, the main hall's seat grid).
        // Mirrors Program.cs: it runs LAST because it configures the content the
        // SQL seed above creates. Idempotent.
        services.GetRequiredService<SIMF.Infrastructure.Seeding.DemoOperationalConfigSeeder>()
            .SeedAsync().GetAwaiter().GetResult();
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
