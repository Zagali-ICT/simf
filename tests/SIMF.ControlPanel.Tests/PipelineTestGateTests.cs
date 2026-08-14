// Guards on the CI test gate in azure-pipelines.yml.
//
// Context: the gate was one all-or-nothing `Run all tests` step, and it was set
// to `enabled: false` on 2026-07-01 because SIMF.Api.Tests is slow. That took
// 922 fast tests off with it, including the Control Panel permission and
// navigation gates in THIS project. The check that catches a missing permission
// existed the whole time; nothing ran it.
//
// It is now split: a fast step listing each project explicitly, and a separate
// step for SIMF.Api.Tests (load-sensitive, needs LocalDB, skipped on PR
// validation). An explicit list is deliberate - it fails loudly when a project
// is renamed, where a glob would silently run one suite fewer. The cost is that
// a NEW test project is silently skipped, which is exactly what these tests
// prevent.
//
// Text assertions over the checked-in YAML; no pipeline is executed.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public class PipelineTestGateTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string Pipeline() =>
        File.ReadAllText(Path.Combine(RepoRoot, "azure-pipelines.yml"));

    /// Every test project under tests/ must be run by SOME stage. A new project
    /// that nothing references is a suite that only ever passes locally.
    ///
    /// <para>READ THIS BEFORE TRUSTING IT. Since D-887 the test steps carry
    /// `condition: eq(parameters.runTests, 'true')` and that parameter DEFAULTS
    /// TO FALSE, so an ordinary run references every suite and executes none of
    /// them. This test therefore proves a project is WIRED UP, not that CI ever
    /// runs it - which is a materially weaker claim than the one it was written
    /// to make. It is kept because the wiring still has to be right for the
    /// `runTests` runs that gate a merge, and because a project dropped from
    /// the list would otherwise never come back when the gates go on.</para>
    [Fact]
    public void Every_test_project_is_referenced_by_the_pipeline()
    {
        var yaml = Pipeline();
        var projects = Directory
            .GetDirectories(Path.Combine(RepoRoot, "tests"))
            .Select(Path.GetFileName)
            .Where(name => name is not null && name.EndsWith(".Tests", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projects);

        var missing = projects
            .Where(name => !yaml.Contains(name!, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These test projects are not referenced anywhere in azure-pipelines.yml, so CI never runs them: "
                + string.Join(", ", missing)
                + ". Add each to the 'Run tests (fast suites)' step, or to a stage of its own.");
    }

    /// No step may be silently switched off. A step that should not always run
    /// gets a `condition` stating its rule; `enabled: false` is invisible in the
    /// run summary and is how the last outage lasted five weeks.
    ///
    /// <para>Widened 2026-08-12 from "test steps" to EVERY step. The old rule
    /// matched a displayName containing "test" or "LocalDB", which left the two
    /// gates that catch the most — 'Convention gate (SIMF-CQP-001)' and the
    /// clean-clone Android compile gate — free to be disabled without a murmur.
    /// The pipeline carries no `enabled: false` today, so the blanket rule costs
    /// nothing and removes the judgement call about which steps "count".</para>
    [Fact]
    public void No_pipeline_step_is_disabled()
    {
        var lines = Pipeline().Split('\n');
        var disabled = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            // The flag as a YAML KEY, not the words inside a comment that warns
            // against using it.
            if (lines[i].TrimStart().StartsWith('#')
                || !lines[i].TrimStart().StartsWith("enabled: false", StringComparison.Ordinal))
            {
                continue;
            }

            // Look back for the step this flag belongs to.
            disabled.Add(
                Enumerable.Range(0, i)
                    .Reverse()
                    .Select(j => lines[j])
                    .FirstOrDefault(l => l.Contains("displayName:", StringComparison.Ordinal))
                    ?.Trim()
                ?? $"(step at line {i + 1})");
        }

        Assert.True(
            disabled.Count == 0,
            "A step is disabled in azure-pipelines.yml. Use a `condition:` that states "
            + "when it should run instead, so the reason is visible in the run summary "
            + "rather than hidden in the YAML:\n"
            + string.Join('\n', disabled));
    }

    /// SIMF.Api.Tests must stay in its OWN step. Measured on one tree: run
    /// beside other work it reports 6 failures in 1h22m; run alone, 0 failures
    /// in 38m20s. Folding it back into the fast step would put it next to the
    /// parallel MobileApp and BadgeDesk stages and reintroduce false reds.
    [Fact]
    public void The_load_sensitive_api_suite_has_its_own_step()
    {
        var yaml = Pipeline();

        var fastStep = yaml.IndexOf("Run tests (fast suites)", StringComparison.Ordinal);
        var apiStep = yaml.IndexOf("Run tests (SIMF.Api.Tests", StringComparison.Ordinal);

        Assert.True(fastStep >= 0, "The 'Run tests (fast suites)' step is missing.");
        Assert.True(apiStep > fastStep, "The SIMF.Api.Tests step is missing or out of order.");

        // Only the fast step's own `projects:` list, not the prose between the
        // two steps, which legitimately names SIMF.Api.Tests when explaining
        // why it is excluded.
        var fastBlock = yaml[fastStep..apiStep];
        var projectLines = fastBlock
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.EndsWith(".csproj", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(projectLines);
        Assert.DoesNotContain(projectLines, l => l.Contains("SIMF.Api.Tests", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every script the pipeline runs must actually be IN the repository.
    ///
    /// <para>The convention gate ran `dart run bin/simf_conventions.dart` for
    /// weeks against a file that was never committed: `.gitignore` carried
    /// `[Bb]in/` for .NET build output, and it also matched
    /// `tool/conventions/bin/` — a DART package entrypoint, i.e. source. The
    /// file existed on the machine that wrote it, so the gate passed there and
    /// nothing looked wrong, while a clean checkout would fail with "Could not
    /// find file". The step that answers the customer's code review could not
    /// run at all.</para>
    ///
    /// <para>This test runs from a clean CI checkout, so a script the pipeline
    /// names but the repository lacks fails the build instead of failing the
    /// gate silently.</para>
    /// </summary>
    [Fact]
    public void Every_script_the_pipeline_runs_exists_in_the_repository()
    {
        var pipeline = Pipeline();

        // `Set-Location '$(Build.SourcesDirectory)/<dir>'` then `dart run <path>`.
        // Resolve each invocation against the most recent Set-Location above it,
        // which is how the agent would resolve it.
        var missing = new List<string>();
        var workingDirectory = string.Empty;

        foreach (var line in pipeline.Split('\n').Select(l => l.Trim()))
        {
            var setLocation = Regex.Match(
                line, @"Set-Location\s+'\$\(Build\.SourcesDirectory\)/(?<dir>[^']+)'");
            if (setLocation.Success)
            {
                workingDirectory = setLocation.Groups["dir"].Value;
                continue;
            }

            var dartRun = Regex.Match(line, @"^dart\s+run\s+(?<script>[^\s]+\.dart)");
            if (!dartRun.Success)
            {
                continue;
            }

            var relative = Path.Combine(workingDirectory, dartRun.Groups["script"].Value)
                .Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(RepoRoot, relative)))
            {
                missing.Add(relative);
            }
        }

        Assert.True(
            missing.Count == 0,
            "azure-pipelines.yml runs a script that is NOT in the repository, so the "
            + "step will fail on a clean checkout even though it passes wherever the "
            + "file happens to exist locally. Check .gitignore — `[Bb]in/` matches any "
            + "directory named bin at any depth, including Dart package entrypoints.\n"
            + string.Join('\n', missing));
    }

    /// <summary>
    /// The pipeline may name ONLY the two Azure DevOps Environments that exist.
    ///
    /// <para>Azure DevOps CREATES an environment the first time a deployment job
    /// names one, so a name matching no real machine does not fail the run - it
    /// silently registers another environment with no resources behind it. The
    /// pipeline named four (`SIMF-Prod-Api`, `-Cp`, `-Web`, `-Edge`) on an
    /// earlier reading of the estate as one server per package, and the portal
    /// duly listed four environments nobody had registered. The estate is two
    /// servers (D-886), so two environments is the whole truth.</para>
    ///
    /// <para>This is the same shape of guard as the anonymous-endpoint
    /// allow-list: the wrong count is invisible in a green run and shows up in
    /// the portal weeks later, so it is pinned to a reviewed set rather than to
    /// a comment.</para>
    /// </summary>
    [Fact]
    public void The_pipeline_deploys_only_to_the_two_real_environments()
    {
        string[] expected = ["Pre-production", "Production"];

        var lines = Pipeline().Split('\n');
        var named = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('#'))
            {
                continue;
            }

            var inline = Regex.Match(line, @"^environment:\s*'?(?<name>[^'#\s]+)'?");
            if (inline.Success)
            {
                named.Add(inline.Groups["name"].Value);
                continue;
            }

            // The full form: `environment:` on its own line, `name:` beneath it.
            if (line != "environment:")
            {
                continue;
            }

            // Only the block that belongs to this key - stop at the next list
            // item, so a `name:` further down the file is never mistaken for
            // this environment's.
            var name = "(unreadable)";
            for (var j = i + 1; j < lines.Length; j++)
            {
                var candidate = lines[j].Trim();
                if (candidate.StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }

                var match = Regex.Match(candidate, @"^name:\s*'?(?<name>[^'#\s]+)'?");
                if (match.Success)
                {
                    name = match.Groups["name"].Value;
                    break;
                }
            }

            named.Add(name);
        }

        Assert.NotEmpty(named);

        var distinct = named.Distinct(StringComparer.Ordinal).ToArray();
        var unexpected = distinct.Except(expected, StringComparer.Ordinal).ToArray();

        Assert.True(
            unexpected.Length == 0,
            "azure-pipelines.yml names an Azure DevOps Environment that is not one of the "
            + "two SIMF has: " + string.Join(", ", unexpected)
            + ". Azure DevOps CREATES an environment on first reference, so this does not "
            + "fail the run - it adds an empty environment to the portal. The estate is two "
            + "servers (D-886): Pre-production and Production. Fix the name, or register the "
            + "new environment and add it here with the machine it binds to.");

        var missing = expected.Except(distinct, StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0,
            "azure-pipelines.yml no longer deploys to: " + string.Join(", ", missing)
            + ". Both environments are deployed by default; a run holds one back with the "
            + "`deployPreProduction` / `deployProduction` parameters at queue time, NOT by "
            + "deleting its job.");
    }

    /// <summary>
    /// Every YAML template the pipeline references must be in the repository.
    ///
    /// <para>The sibling check above does this for `dart run` scripts, after the
    /// convention gate spent weeks pointing at a file `.gitignore` had excluded.
    /// A `- template:` reference fails the same way and is worse: the pipeline
    /// does not compile at all, so nothing runs, including the gates that would
    /// have reported it.</para>
    /// </summary>
    [Fact]
    public void Every_yaml_template_the_pipeline_references_exists_in_the_repository()
    {
        var missing = Pipeline()
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith('#'))
            .Select(l => Regex.Match(l, @"^-\s*template:\s*(?<path>[^\s@#]+\.ya?ml)"))
            .Where(m => m.Success)
            .Select(m => m.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar))
            .Where(relative => !File.Exists(Path.Combine(RepoRoot, relative)))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "azure-pipelines.yml references a YAML template that is NOT in the repository, "
            + "so the pipeline will not compile and no stage runs at all:\n"
            + string.Join('\n', missing));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
        }
        return dir.FullName;
    }
}
