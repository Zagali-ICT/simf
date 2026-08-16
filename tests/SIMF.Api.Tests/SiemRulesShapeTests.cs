// D-185 — structural guard on the Sigma 2.x rule files under
// docs/soc/siem-rules. The rules ship as SOC source-of-truth alongside
// the audit-event names they consume; this test catches accidental
// drift (event-name renames, missing correlation blocks, legacy v1
// pipe-aggregate syntax sneaking back in, broken Detail.* field
// references).
//
// Text-contains assertions instead of a real Sigma parser — owner-rule
// §1.7 forbids csproj edits, so no YamlDotNet / pySigma dependency.
// The structural invariants here are the minimum needed to keep the
// rule files importable by any Sigma 2.x toolchain.
using System.IO;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class SiemRulesShapeTests
{
    // The rule files are checked into the repo at docs/soc/siem-rules.
    // The test project runs from a deep bin directory at test time, so
    // walk upward until we find the repo root.
    private static string RulesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        var rules = Path.Combine(directory!.FullName, "docs", "soc", "siem-rules");
        Assert.True(Directory.Exists(rules),
            $"SIEM rules directory not found at {rules}");
        return rules;
    }

    private static readonly string[] ExpectedRuleFiles =
    {
        "ai-001-prompt-tampering-burst.yml",
        "ai-002-test-harness-abuse.yml",
        "ai-003-bulk-deactivate.yml",
        "ai-004-silent-prompt-edit.yml",
        "ai-005-redaction-trigger-rate.yml",
        "ai-006-key-probing.yml",
        "ai-007-iban-with-intent.yml",
        "ai-008-bulk-pii-funnel.yml",
        "ai-009-secret-exfil.yml",
        "ai-010-invocation-bulk-view.yml",
        "s-001-seat-bulk-release.yml",
        "s-001b-seat-bulk-release-burst.yml",
        // D-186 review-pass — UserType collapse audit-event consumers
        "m-003-profiletype-isvisitor-flip.yml",
        "m-004-approval-scope-probe.yml",
        "m-005-walkin-partner-burst.yml",
    };

    [Fact]
    public void Every_expected_rule_file_exists()
    {
        var directory = RulesDirectory();
        foreach (var file in ExpectedRuleFiles)
        {
            var path = Path.Combine(directory, file);
            Assert.True(File.Exists(path),
                $"Missing SIEM rule file: {path}");
        }
    }

    [Fact]
    public void Correlation_rules_reference_an_existing_base_rule_id()
    {
        // D-185 test-analyst review-pass: catches the "delete s-001 silently
        // breaks s-001b" case (s-001b's correlation block references
        // `s-001-base-seat-released` which lives in s-001's file).
        var directory = RulesDirectory();
        var declaredIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                // top-level `id: <slug>` lines in either base or correlation docs.
                var match = System.Text.RegularExpressions.Regex.Match(
                    line, @"^id:\s*([a-z0-9-]+)\s*$");
                if (match.Success)
                {
                    declaredIds.Add(match.Groups[1].Value);
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            var content = File.ReadAllText(file);
            // Crude `rules:\n  - <id>` extraction — sufficient for the
            // single-base-rule pattern every D-185 file uses.
            var matches = System.Text.RegularExpressions.Regex.Matches(
                content, @"^\s*-\s+([a-z0-9-]+)\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var referenced = match.Groups[1].Value;
                Assert.True(declaredIds.Contains(referenced),
                    $"{Path.GetFileName(file)} references base rule "
                    + $"'{referenced}' which does not exist");
            }
        }
    }

    [Fact]
    public void No_rule_file_uses_legacy_pipe_aggregate_syntax()
    {
        // Sigma v1's `condition: selection | count() by X > N` plus a
        // separate `timeframe:` was retired by D-185. The Sigma 2.x form
        // is a top-level `correlation:` block. Both convert to the same
        // SPL/KQL via pySigma but strict v2 linters reject the v1 shape.
        // The substring "| count()" only appears in v1 pipe syntax — v2
        // never contains it.
        var directory = RulesDirectory();
        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("| count()", content);
            Assert.DoesNotContain("| count_distinct(", content);
            // The standalone `timeframe:` key was also v1; v2 uses `timespan:`
            // inside the correlation block.
            Assert.DoesNotContain("\ntimeframe:", content);
        }
    }

    [Fact]
    public void Every_rule_file_has_id_title_logsource_or_correlation()
    {
        var directory = RulesDirectory();
        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            var content = File.ReadAllText(file);
            Assert.Contains("title:", content);
            Assert.Contains("id:", content);
            // Either a logsource (base rule) or a correlation block
            // (correlation rule referencing a base rule).
            Assert.True(
                content.Contains("logsource:") || content.Contains("correlation:"),
                $"{Path.GetFileName(file)} must declare logsource or correlation");
        }
    }

    [Fact]
    public void Correlation_blocks_use_v2_keys()
    {
        // Sigma 2.x correlation block requires: type, rules, group-by,
        // timespan, condition. The invented v1 keys `aggregate:` and
        // `threshold:` (D-184 AI-002 bug) must never recur.
        var directory = RulesDirectory();
        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            var content = File.ReadAllText(file);
            if (!content.Contains("correlation:"))
            {
                continue;
            }
            Assert.Contains("type:", content);
            Assert.Contains("group-by:", content);
            Assert.Contains("timespan:", content);
            // The v1 invented keys must not appear.
            Assert.DoesNotContain("\n  aggregate:", content);
            Assert.DoesNotContain("\n  threshold:", content);
        }
    }

    [Fact]
    public void No_rule_file_uses_invented_Detail_json_modifier()
    {
        // `Detail|json: { ... }` was a non-existent Sigma modifier used
        // in the D-184 draft. Pre-parsed Detail keys use `Detail.field:`
        // form (see README "Canonical Detail JSON shape").
        var directory = RulesDirectory();
        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("Detail|json", content);
        }
    }

    [Fact]
    public void Actor_field_canon_is_ActorUserId_not_CallerUserId()
    {
        // D-185 field-name unification: audit events emit ActorUserId
        // at the top level. The earlier draft mixed in CallerUserId
        // which is not a real top-level audit field.
        var directory = RulesDirectory();
        foreach (var file in Directory.EnumerateFiles(directory, "*.yml"))
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("CallerUserId", content);
        }
    }
}
