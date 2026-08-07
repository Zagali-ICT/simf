// D-869 — changing SuperAdmin:Email against an existing database seeds a SECOND
// super-admin rather than moving the first.
//
// The seeder resolves the super-admin BY E-MAIL, so a changed address matches
// nothing and falls through to CreateSuperAdminAsync. The previous account keeps
// the Administrator role — the `perm:*` wildcard, the highest privilege in the
// system — and keeps signing in with its old credentials. Nothing in the boot
// path said so, which is how the D-868 domain migration produced two
// full-privilege accounts on a live database without anyone noticing.
//
// These tests drive the real seeder through the same substitution the sibling
// SuperAdminSeedFailureTests uses: the live scope with only the SuperAdmin
// options and IHostEnvironment replaced.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SuperAdminDuplicateSeedTests : IClassFixture<SimfApiFactory>
{
    // Satisfies the Identity policy, so the account really is created and the
    // duplicate this test is about actually comes into existence.
    private const string ValidPassword = "Sup3r!Admin#Seed9";

    private readonly SimfApiFactory _factory;

    public SuperAdminDuplicateSeedTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>A duplicate super-admin must reach the audit trail, not just the
    /// startup log. A log line scrolls away; a second unattended account holding
    /// the wildcard is exactly what a security review has to be able to find
    /// after the fact.</summary>
    [Fact]
    public async Task Seeding_a_new_super_admin_email_audits_the_duplicate_it_creates()
    {
        var newEmail = $"superadmin-moved-{Guid.NewGuid():N}@simf.test";

        await RunSeedAsync(newEmail);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var audit = await appDb.OperationLog
            .AsNoTracking()
            .Where(entry => entry.EventType == AuditEvents.SuperAdminDuplicateSeeded
                            && entry.SubjectEmail == newEmail)
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);

        // Failure, not Success: the seed completed, but it left the system in a
        // state nobody asked for. Filing it as Success would hide it in exactly
        // the report a reviewer runs.
        Assert.Equal(AuditOutcome.Failure, audit!.Outcome);

        // The operator cannot act on "a duplicate exists" — the entry has to name
        // the account that is now redundant.
        Assert.False(string.IsNullOrWhiteSpace(audit.Detail));
        Assert.Contains("Administrator", audit.Detail!, StringComparison.Ordinal);
    }

    /// <summary>The counterpart: re-seeding the SAME address is the normal boot
    /// path and must stay silent, or the audit trail fills with noise on every
    /// restart and the real event becomes unfindable.</summary>
    [Fact]
    public async Task Re_seeding_the_same_super_admin_email_audits_nothing()
    {
        var email = $"superadmin-stable-{Guid.NewGuid():N}@simf.test";

        // First boot creates it — a duplicate entry here is expected, because
        // the fixture's own super-admin already holds the Administrator role.
        await RunSeedAsync(email);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var before = await appDb.OperationLog
            .CountAsync(entry => entry.EventType == AuditEvents.SuperAdminDuplicateSeeded);

        // Second boot finds the account by e-mail and must not report anything.
        await RunSeedAsync(email);

        var after = await appDb.OperationLog
            .CountAsync(entry => entry.EventType == AuditEvents.SuperAdminDuplicateSeeded);

        Assert.Equal(before, after);
    }

    private async Task RunSeedAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var options = Options.Create(new SuperAdminOptions
        {
            Email = email,
            TempPassword = ValidPassword,
            // No TOTP secret: pairing is a separate concern and the seeder only
            // pairs one when it is configured.
            TotpSecret = string.Empty,
            PasswordChangeRequired = true,
        });

        var seeder = ActivatorUtilities.CreateInstance<IdentitySeeder>(
            scope.ServiceProvider,
            options,
            new StubHostEnvironment(Environments.Development));
        await seeder.SeedAsync();
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SIMF.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
