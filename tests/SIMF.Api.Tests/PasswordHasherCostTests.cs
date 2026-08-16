using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Pins the test-only password-hasher work factor at both ends: it must be low
/// HERE, and it must stay unconfigured — that is, at the framework default — in
/// production.
/// </summary>
/// <remarks>
/// PBKDF2 is meant to be slow, and the suite performs on the order of 5,000
/// hash-or-verify operations per run (roughly ten seeding each fixture, plus one
/// per user a test creates and one per sign-in it drives). Measured on this code
/// with the framework default of 100,000 iterations: 33 ms per operation. At
/// 1,000 iterations: 0.3 ms. That is minutes per run spent proving nothing.
///
/// The reason this is a test and not a comment is the direction of the danger.
/// Losing the override only makes the suite slow again, and nothing else would
/// report it. Copying the override into production would quietly weaken every
/// stored password in the product, and nothing else would report that either.
/// </remarks>
[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class PasswordHasherCostTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public PasswordHasherCostTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public void The_test_host_hashes_with_the_reduced_work_factor()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<PasswordHasherOptions>>().Value;

        Assert.Equal(1000, options.IterationCount);
    }

    [Fact]
    public void Production_code_never_configures_the_password_hasher()
    {
        var source = LocateSourceFolder();
        var offenders = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file)
                .Contains("PasswordHasherOptions", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(source, file))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "PasswordHasherOptions is configured under src/, which means the shipped "
            + "product is no longer using the framework's password-hashing work factor. "
            + "The low iteration count belongs to the TEST HOST only "
            + "(SimfApiFactory.ConfigureWebHost). If this was a deliberate decision to "
            + "change the production work factor, it needs an owner decision and this "
            + "test updated in the same changeset. Files: "
            + string.Join(", ", offenders));
    }

    /// <summary>Walks up from the test binaries to the repository's src/ folder.</summary>
    private static string LocateSourceFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository's src/ folder from "
            + AppContext.BaseDirectory);
    }
}
