// Guards on the deployment environment scripts + the controlled documents that
// describe them.
//
// Context: an earlier attempt to make the API's set-env script shareable tried
// to DELETE its .gitignore entry, which would have committed a live SQL
// connection string, an SMTP app password and several production key literals.
// The safe shape is a separate, differently-named TEMPLATE with every value
// empty, while the filled overlay stays ignored. These tests pin that shape so
// the dangerous version cannot come back:
//
//   * deploy/set-env-api.template.ps1 is tracked and every value is EMPTY;
//   * .gitignore STILL ignores deploy/set-env-api.ps1;
//   * deploy/configure-prod-env.ps1 never overwrites an existing encryption key.
//
// Text assertions over the checked-in files (no PowerShell is executed and the
// runbook is never run) — owner-rule section 1.7 forbids csproj edits, so no
// extra package. This file commits no secret: it asserts on key NAMES and on
// the emptiness of values, never on a value.
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DeploymentEnvTemplateTests
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

    private static string ReadRepoFile(params string[] segments)
    {
        var path = Path.Combine(new[] { RepoRoot() }.Concat(segments).ToArray());
        Assert.True(File.Exists(path), $"Expected file not found: {string.Join('/', segments)}");
        return File.ReadAllText(path);
    }

    private const string TemplateName = "set-env-api.template.ps1";
    private const string RunbookName = "configure-prod-env.ps1";

    // Matches an assignment line inside the $vars hashtable:  "NAME" = "value"
    private static readonly Regex AssignmentPattern =
        new("^\\s*\"(?<name>[A-Za-z0-9_]+)\"\\s*=\\s*\"(?<value>[^\"]*)\"",
            RegexOptions.Multiline | RegexOptions.Compiled);

    // -------------------------------------------------------------------
    // E1 — the template
    // -------------------------------------------------------------------

    [Fact]
    public void The_api_env_template_is_tracked_under_its_own_template_name()
    {
        var template = ReadRepoFile("deploy", TemplateName);

        Assert.Contains("#Requires -RunAsAdministrator", template, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariableTarget]::Machine", template, StringComparison.Ordinal);

        // The "an empty value is SKIPPED with a warning" behaviour the two
        // sibling scripts use, so an unedited template never sets blanks.
        Assert.Contains("IsNullOrWhiteSpace", template, StringComparison.Ordinal);
        Assert.Contains("SKIP (empty)", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_value_in_the_api_env_template_is_empty()
    {
        var template = ReadRepoFile("deploy", TemplateName);
        var assignments = AssignmentPattern.Matches(template);

        Assert.True(
            assignments.Count > 20,
            $"Only {assignments.Count} variable assignments were parsed out of "
            + $"{TemplateName}; the template or the parser is wrong.");

        var populated = assignments
            .Where(match => match.Groups["value"].Value.Length > 0)
            .Select(match => match.Groups["name"].Value)
            .ToList();

        Assert.True(
            populated.Count == 0,
            $"{TemplateName} must ship EVERY value empty — it is a committed "
            + "template and a populated value is a committed credential. "
            + "Populated: " + string.Join(", ", populated));
    }

    [Fact]
    public void The_api_env_template_covers_every_required_key()
    {
        var template = ReadRepoFile("deploy", TemplateName);

        // The minimum set: the two databases, the token key, the three
        // Production boot gates, the meeting-link origin, the two seed
        // passwords, the AI keys and the storage paths.
        foreach (var key in new[]
                 {
                     "SIMF_ConnectionStrings__SimfIdentityDb",
                     "SIMF_ConnectionStrings__SimfAppDb",
                     "SIMF_Jwt__SigningKey",
                     "SIMF_FileStorage__EncryptionKey",
                     "SIMF_Storage__UserIdDocumentEncryptionKey",
                     "SIMF_Ai__PromptHash__Secret",
                     "SIMF_MeetingLinks__PublicWebBaseUrl",
                     "SIMF_SuperAdmin__TempPassword",
                     "SIMF_Seed__DemoPassword",
                     "SIMF_Ai__Gemini__ApiKey",
                     "SIMF_Ai__Anthropic__ApiKey",
                     "SIMF_Ai__OpenAi__ApiKey",
                     "SIMF_Storage__AvatarBase",
                     "SIMF_Storage__UserIdDocumentBase",
                     "SIMF_Storage__LogDirectory",
                     "SIMF_FileStorage__RootPath",
                 })
        {
            Assert.True(
                template.Contains(key, StringComparison.Ordinal),
                $"{TemplateName} is missing the required key {key}.");
        }
    }

    [Fact]
    public void The_template_documents_what_breaks_when_a_boot_gate_is_missing()
    {
        var template = ReadRepoFile("deploy", TemplateName);

        // FileStorage:EncryptionKey missing => the API does not boot. The exact
        // message comes from AesGcmEnvelopeCipher.DecodeKey.
        Assert.Contains(
            "Configuration value 'FileStorage:EncryptionKey'",
            template,
            StringComparison.Ordinal);

        // Rotating it strands every already-stored file.
        Assert.Contains("undecryptable", template, StringComparison.Ordinal);

        // MeetingLinks:PublicWebBaseUrl empty => the Approve / Resend actions
        // are refused UP FRONT with a bilingual 409, not silently skipped.
        Assert.Contains("MEETING_LINKS_NOT_CONFIGURED", template, StringComparison.Ordinal);
    }

    /// <summary>The load-bearing safety property. The filled overlay carries
    /// real production credentials and MUST stay ignored; the tracked, shareable
    /// artefact is the separate template.</summary>
    [Fact]
    public void The_filled_api_env_script_is_still_gitignored()
    {
        var gitignore = ReadRepoFile(".gitignore");

        Assert.True(
            gitignore.Contains("deploy/set-env-api.ps1", StringComparison.Ordinal),
            "deploy/set-env-api.ps1 must STAY in .gitignore — it is the filled "
            + "overlay holding the production SQL connection string, the SMTP "
            + "app password and the key literals. To share the variable list, "
            + $"edit deploy/{TemplateName} instead; never un-ignore the "
            + "filled script.");
    }

    // -------------------------------------------------------------------
    // E2 — the runbook
    // -------------------------------------------------------------------

    [Fact]
    public void The_production_runbook_generates_keys_without_overwriting_them()
    {
        var runbook = ReadRepoFile("deploy", RunbookName);

        Assert.Contains("#Requires -RunAsAdministrator", runbook, StringComparison.Ordinal);
        Assert.Contains(
            "System.Security.Cryptography.RandomNumberGenerator",
            runbook,
            StringComparison.Ordinal);

        // The never-overwrite guard: it must test for an existing value and
        // skip rather than replace it.
        Assert.Contains("PRESERVED (already set)", runbook, StringComparison.Ordinal);
        Assert.Contains("Refusing to overwrite", runbook, StringComparison.Ordinal);

        // No escape hatch: a -Force switch would make the single most
        // destructive operation in the deployment a one-flag mistake.
        Assert.DoesNotContain("[switch]$Force", runbook, StringComparison.Ordinal);

        // Prompts must not echo, and the verify pass must report state only.
        Assert.Contains("-AsSecureString", runbook, StringComparison.Ordinal);
        Assert.Contains("[MISSING]", runbook, StringComparison.Ordinal);

        // It finishes by restarting the pools and health-checking the API.
        Assert.Contains("Restart-WebAppPool", runbook, StringComparison.Ordinal);
        Assert.Contains("/health", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void The_deploy_readme_documents_the_scripts_together()
    {
        var readme = ReadRepoFile("deploy", "README.md");

        foreach (var script in new[]
                 {
                     TemplateName,
                     RunbookName,
                     "set-env-cp.ps1",
                     "set-env-web.ps1",
                 })
        {
            Assert.True(
                readme.Contains(script, StringComparison.Ordinal),
                $"deploy/README.md does not mention {script}.");
        }
    }

    [Fact]
    public void The_cp_and_web_scripts_do_not_claim_a_fresh_clone_has_the_api_script()
    {
        // Both used to say "set-env-api.ps1 already sets these at Machine
        // scope", which a fresh clone can never satisfy — that file is ignored.
        foreach (var script in new[] { "set-env-cp.ps1", "set-env-web.ps1" })
        {
            var text = ReadRepoFile("deploy", script);
            Assert.False(
                text.Contains("set-env-api.ps1 already sets these", StringComparison.Ordinal),
                $"deploy/{script} still claims the ignored set-env-api.ps1 "
                + "already sets these variables; point at "
                + $"deploy/{TemplateName} instead.");
        }
    }

    // -------------------------------------------------------------------
    // E3 — the controlled document
    // -------------------------------------------------------------------

    [Fact]
    public void Fds001_records_oi3_as_closed_by_the_website_auth_removal()
    {
        var fds = ReadRepoFile("docs", "SIMF-FDS-001-Authentication-and-Login.md");

        Assert.False(
            fds.Contains(
                "OI-3 | Confirm whether the website offers sign-in",
                StringComparison.Ordinal),
            "SIMF-FDS-001 still lists OI-3 as an OPEN item, but the owner "
            + "decided on 2026-07-27 (D-774) that the Website ships no "
            + "sign-in. Close it with the decision, the date and the "
            + "decisions-log pointer.");

        Assert.Contains("D-774", fds, StringComparison.Ordinal);

        // The old claim that the Website offers a sign-in surface must be gone.
        Assert.DoesNotContain(
            "The website sign-in, where the site offers it",
            fds,
            StringComparison.Ordinal);
    }
}
