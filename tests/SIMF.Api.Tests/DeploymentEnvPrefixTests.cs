// Guards on the per-application environment prefixes.
//
// Until 2026-08-12 every SIMF host read one shared prefix, SIMF_. Machine scope
// is shared by every process on a box, so two SIMF applications on one server
// could not be given different values for the same key: there was a single
// SIMF_Storage__LogDirectory and a single SIMF_Api__BaseUrl between them, shared
// whether that was wanted or not. Each host now reads its own prefix.
//
// Two things need pinning. That each host actually registers its own prefix -
// a copy-paste leaving the Website on SIMF_CP_ would compile, start, and read
// the Control Panel's settings. And that the legacy guard behaves, since it is
// what turns a half-finished upgrade into a named error instead of a confusing
// one about a missing encryption key.
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class DeploymentEnvPrefixTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    /// <summary>Each host registers its OWN prefix, and exactly one.</summary>
    [Theory]
    [InlineData("src/Backend/SIMF.Api/Program.cs", "SIMF_API_")]
    [InlineData("src/ControlPanel/SIMF.ControlPanel/Program.cs", "SIMF_CP_")]
    [InlineData("src/Website/SIMF.Web/Program.cs", "SIMF_WEB_")]
    [InlineData("src/Edge/SIMF.MobileEdge/Program.cs", "SIMF_EDGE_")]
    public void Each_host_registers_its_own_environment_prefix(
        string relativePath, string expectedPrefix)
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var registrations = Regex.Matches(
            source, @"AddEnvironmentVariables\(""(?<prefix>[^""]*)""\)");

        Assert.True(
            registrations.Count == 1,
            $"{relativePath} registers {registrations.Count} prefixed environment "
            + "sources; exactly one is expected, or which one wins depends on order.");

        Assert.Equal(expectedPrefix, registrations[0].Groups["prefix"].Value);
    }

    /// <summary>No host may go back to the shared prefix. It compiles and starts
    /// perfectly well - and silently reintroduces the collision, because the
    /// value it then reads is whichever application last wrote that name.</summary>
    [Theory]
    [InlineData("src/Backend/SIMF.Api/Program.cs")]
    [InlineData("src/ControlPanel/SIMF.ControlPanel/Program.cs")]
    [InlineData("src/Website/SIMF.Web/Program.cs")]
    [InlineData("src/Edge/SIMF.MobileEdge/Program.cs")]
    public void No_host_registers_the_shared_prefix(string relativePath)
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain(
            @"AddEnvironmentVariables(""SIMF_"")", source, StringComparison.Ordinal);
    }

    /// <summary>The four prefixes are distinct. Two hosts sharing one would read
    /// each other's settings, which is the failure the split exists to remove.</summary>
    [Fact]
    public void The_four_prefixes_are_distinct()
    {
        var prefixes = new[]
            {
                ("src/Backend/SIMF.Api/Program.cs", "SIMF_API_"),
                ("src/ControlPanel/SIMF.ControlPanel/Program.cs", "SIMF_CP_"),
                ("src/Website/SIMF.Web/Program.cs", "SIMF_WEB_"),
                ("src/Edge/SIMF.MobileEdge/Program.cs", "SIMF_EDGE_"),
            }
            .Select(entry => entry.Item2)
            .ToList();

        Assert.Equal(prefixes.Count, prefixes.Distinct(StringComparer.Ordinal).Count());
    }

    // ---------------------------------------------------------------
    // The legacy guard
    // ---------------------------------------------------------------

    [Fact]
    public void The_guard_reports_a_pre_split_variable()
    {
        var legacy = SimfLegacyEnvironmentGuard.FindLegacyNames(
            new[] { "SIMF_ConnectionStrings__SimfAppDb", "PATH" }, "SIMF_API_");

        Assert.Equal(new[] { "SIMF_ConnectionStrings__SimfAppDb" }, legacy);
    }

    /// <summary>A current name also begins with "SIMF_", so a naive prefix test
    /// would report every correctly-provisioned server as broken.</summary>
    [Fact]
    public void The_guard_ignores_every_current_per_application_name()
    {
        var legacy = SimfLegacyEnvironmentGuard.FindLegacyNames(
            new[]
            {
                "SIMF_API_ConnectionStrings__SimfAppDb",
                "SIMF_CP_Api__BaseUrl",
                "SIMF_WEB_Storage__LogDirectory",
                "SIMF_EDGE_ReverseProxy__KnownProxies__0",
                "ASPNETCORE_ENVIRONMENT",
            },
            "SIMF_API_");

        Assert.Empty(legacy);
    }

    /// <summary>Harness variables are not application config and are never
    /// re-provisioned by set-env, so flagging them would fire the guard on a
    /// developer box for no reason.</summary>
    [Fact]
    public void The_guard_ignores_the_test_and_smoke_harness_variables()
    {
        var legacy = SimfLegacyEnvironmentGuard.FindLegacyNames(
            new[] { "SIMF_TEST_DEMO_PASSWORD", "SIMF_SMOKE_EMAIL" }, "SIMF_API_");

        Assert.Empty(legacy);
    }

    /// <summary>The message has to name what was found and what to run; a guard
    /// that only says "misconfigured" moves the puzzle rather than solving it.
    /// Asserted on the builder rather than on Verify, which reads the real
    /// environment and would only throw on an agent that happened to be
    /// mis-provisioned.</summary>
    [Fact]
    public void The_guard_names_the_variable_and_the_fix()
    {
        var message = SimfLegacyEnvironmentGuard.BuildMessage(
            new[] { "SIMF_Jwt__SigningKey" }, "SIMF_API_");

        // What was found.
        Assert.Contains("SIMF_Jwt__SigningKey", message, StringComparison.Ordinal);
        // What this build reads instead.
        Assert.Contains("SIMF_API_", message, StringComparison.Ordinal);
        // What to run.
        Assert.Contains("set-env-", message, StringComparison.Ordinal);
        Assert.Contains("clean-env.ps1", message, StringComparison.Ordinal);
    }

    /// <summary>A long list is truncated rather than printed whole - a server
    /// mid-upgrade carries sixty of these, and a wall of names buries the fix.</summary>
    [Fact]
    public void The_guard_truncates_a_long_list()
    {
        var many = Enumerable.Range(0, 20).Select(i => $"SIMF_Section__Key{i}").ToList();

        var message = SimfLegacyEnvironmentGuard.BuildMessage(many, "SIMF_API_");

        Assert.Contains("20 environment variable(s)", message, StringComparison.Ordinal);
        Assert.Contains("...", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SIMF_Section__Key19", message, StringComparison.Ordinal);
    }
}
