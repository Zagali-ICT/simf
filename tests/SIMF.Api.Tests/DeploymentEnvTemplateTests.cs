// Guards on the deployment environment script + the controlled documents that
// describe it.
//
// Context: an earlier attempt to make the env script shareable tried to DELETE
// its .gitignore entry, which would have committed a live SQL connection
// string, an SMTP app password and several production key literals. The safe
// shape is a separate, differently-named TEMPLATE, while the filled overlay
// stays ignored. These tests pin that shape so the dangerous version cannot
// come back:
//
//   * deploy/set-env.template.ps1 is tracked and every SECRET value is EMPTY;
//   * .gitignore STILL ignores the filled overlays;
//   * deploy/configure-prod-env.ps1 never overwrites an existing encryption key.
//
// On 2026-08-06 the three per-service scripts (set-env-api.template.ps1,
// set-env-cp.ps1, set-env-web.ps1) were merged into the single template, so a
// deployment is "publish, run one script, restart the pools". The bright line
// moved from "every value is empty" to "every SECRET is empty": an all-empty
// template meant hand-filling ~60 variables, which is not a deployment step
// anybody performs reliably. It stays mechanically checkable because each entry
// carries its own Secret = $true/$false flag rather than leaving the reader to
// judge which is which.
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

    private const string TemplateName = "set-env.template.ps1";
    private const string RunbookName = "configure-prod-env.ps1";

    // Matches one entry of the $vars array:
    //   [pscustomobject]@{ Name = "X"; Value = "Y"; Secret = $false; ... }
    private static readonly Regex EntryPattern =
        new("""Name\s*=\s*"(?<name>[A-Za-z0-9_]+)"\s*;\s*Value\s*=\s*"(?<value>[^"]*)"\s*;\s*Secret\s*=\s*\$(?<secret>true|false)""",
            RegexOptions.Multiline | RegexOptions.Compiled);

    // -------------------------------------------------------------------
    // E1 — the template
    // -------------------------------------------------------------------

    [Fact]
    public void The_env_template_is_tracked_under_its_own_template_name()
    {
        var template = ReadRepoFile("deploy", TemplateName);

        Assert.Contains("#Requires -RunAsAdministrator", template, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariableTarget]::Machine", template, StringComparison.Ordinal);

        // The "an empty value is SKIPPED with a warning" behaviour, so an
        // unedited template never blanks a working server.
        Assert.Contains("IsNullOrWhiteSpace", template, StringComparison.Ordinal);
        Assert.Contains("SKIP (empty", template, StringComparison.Ordinal);
    }

    /// <summary>The load-bearing safety property. A non-secret default may ship
    /// filled — that is the point of merging the three scripts — but a secret
    /// never may.</summary>
    [Fact]
    public void Every_secret_in_the_env_template_is_empty()
    {
        var template = ReadRepoFile("deploy", TemplateName);
        var entries = EntryPattern.Matches(template);

        Assert.True(
            entries.Count > 50,
            $"Only {entries.Count} variable entries were parsed out of {TemplateName}; "
            + "the template or the parser is wrong.");

        var populatedSecrets = entries
            .Where(m => m.Groups["secret"].Value == "true")
            .Where(m => m.Groups["value"].Value.Length > 0)
            .Select(m => m.Groups["name"].Value)
            .ToList();

        Assert.True(
            populatedSecrets.Count == 0,
            $"{TemplateName} must ship EVERY Secret = $true value empty — it is a "
            + "committed template and a populated secret is a committed credential. "
            + "Populated: " + string.Join(", ", populatedSecrets));
    }

    /// <summary>Every secret the deployment needs must be declared, even though
    /// it ships empty; an undeclared secret is one an operator never sets.</summary>
    [Fact]
    public void The_env_template_declares_the_known_secrets_as_secret()
    {
        var template = ReadRepoFile("deploy", TemplateName);
        var secrets = EntryPattern.Matches(template)
            .Where(m => m.Groups["secret"].Value == "true")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in new[]
                 {
                     "SIMF_ConnectionStrings__SimfIdentityDb",
                     "SIMF_ConnectionStrings__SimfAppDb",
                     "SIMF_Jwt__SigningKey",
                     "SIMF_FileStorage__EncryptionKey",
                     "SIMF_Storage__UserIdDocumentEncryptionKey",
                     "SIMF_Ai__PromptHash__Secret",
                     "SIMF_SuperAdmin__TempPassword",
                     "SIMF_SuperAdmin__TotpSecret",
                     "SIMF_Email__Password",
                     "SIMF_Swagger__Password",
                     "SIMF_WalkInMode__BadgeKey",
                 })
        {
            Assert.True(
                secrets.Contains(name),
                $"{TemplateName} must declare {name} with Secret = $true, so the "
                + "emptiness guard covers it.");
        }
    }

    [Fact]
    public void The_env_template_covers_every_required_key_for_all_three_apps()
    {
        var template = ReadRepoFile("deploy", TemplateName);

        // The minimum set across SimfAPI, SimfCP and SimfWeb: the two databases,
        // the token key, the three Production boot gates, the file-store root,
        // the meeting-link origin, the seed passwords, the AI keys, and the two
        // settings the CP and Website need to reach the API.
        //
        // Storage__AvatarBase and Storage__UserIdDocumentBase were required here
        // until 2026-08-05. They named the roots of bespoke filesystem stores
        // that D-568 replaced with the unified StoredFile store, and by then
        // nothing read either one. FileStorage__RootPath is what actually
        // decides where the bytes land, and it is required below.
        foreach (var key in new[]
                 {
                     "ASPNETCORE_ENVIRONMENT",
                     "SIMF_ConnectionStrings__SimfIdentityDb",
                     "SIMF_ConnectionStrings__SimfAppDb",
                     "SIMF_Jwt__SigningKey",
                     "SIMF_FileStorage__EncryptionKey",
                     "SIMF_FileStorage__RootPath",
                     "SIMF_Storage__UserIdDocumentEncryptionKey",
                     "SIMF_Ai__PromptHash__Secret",
                     "SIMF_MeetingLinks__PublicWebBaseUrl",
                     "SIMF_SuperAdmin__TempPassword",
                     "SIMF_Seed__DemoPassword",
                     "SIMF_Ai__Gemini__ApiKey",
                     "SIMF_Ai__Anthropic__ApiKey",
                     "SIMF_Ai__OpenAi__ApiKey",
                     "SIMF_Storage__LogDirectory",
                     "SIMF_Api__BaseUrl",
                     "SIMF_Api__AllowSelfSignedCertificate",
                     "SIMF_Session__LifetimeHours",
                 })
        {
            Assert.True(
                template.Contains(key, StringComparison.Ordinal),
                $"{TemplateName} is missing the required key {key}.");
        }
    }

    private static string SingleValue(MatchCollection entries, string name)
    {
        var values = entries
            .Where(m => m.Groups["name"].Value == name)
            .Select(m => m.Groups["value"].Value)
            .ToList();

        Assert.True(
            values.Count == 1,
            $"{TemplateName} should declare {name} exactly once; found {values.Count}.");
        return values[0];
    }

    /// <summary>The template's VALUES, not just its key names. Api:BaseUrl and
    /// Api:AllowSelfSignedCertificate have to move together: the flag installs
    /// DangerousAcceptAnyServerCertificateValidator on the typed clients that
    /// forward the admin password, the TOTP code and the perm:* bearer token, so
    /// pairing it with a public origin hands those to whoever answers for that
    /// name. SimfApiBaseAddress.Resolve refuses the pairing at boot on the
    /// EFFECTIVE config; this stops the repo recommending it in the first place.
    /// A server provisioned from an older copy of the overlay is unaffected by a
    /// template correction, which is why both layers exist. The key-presence
    /// test above passed throughout the window where the values were wrong.</summary>
    [Fact]
    public void The_env_template_never_pairs_the_trust_all_with_a_public_origin()
    {
        var template = ReadRepoFile("deploy", TemplateName);
        var entries = EntryPattern.Matches(template);

        var baseUrl = SingleValue(entries, "SIMF_Api__BaseUrl");
        var allowSelfSigned = SingleValue(entries, "SIMF_Api__AllowSelfSignedCertificate");

        // bool.TryParse, not a compare against "true", because that is what
        // GetValue<bool> does at runtime: it trims, so " true " binds to true and
        // installs the bypass while a string compare would call it disabled.
        var trustAll = bool.TryParse(allowSelfSigned, out var parsed) && parsed;
        var loopback = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback;

        Assert.True(
            !trustAll || loopback,
            $"{TemplateName} pairs SIMF_Api__AllowSelfSignedCertificate=true with "
            + $"SIMF_Api__BaseUrl='{baseUrl}', which is not a loopback address. That "
            + "disables certificate validation on a public origin and exposes the "
            + "forwarded bearer token to an on-path attacker. Set the flag to false, "
            + "or point BaseUrl back at loopback.");
    }

    /// <summary>Windows PowerShell 5.1 decodes a BOM-less file as the ANSI
    /// codepage, so one em dash in a comment becomes three characters - and one
    /// of them is a quote, which terminates the string and stops the script
    /// parsing. The template carried six em dashes and one ellipsis, and failed
    /// with 8 parse errors under powershell.exe until 2026-08-07, while the same
    /// file was fine under pwsh 7 (UTF-8 by default). An operator on Windows
    /// Server therefore ran it and set NOTHING. Pure ASCII is the fix that does
    /// not depend on a BOM surviving an editor round-trip.</summary>
    [Theory]
    [InlineData(TemplateName)]
    [InlineData(RunbookName)]
    public void The_operator_run_scripts_are_pure_ascii_so_windows_powershell_can_parse_them(
        string scriptName)
    {
        var script = ReadRepoFile("deploy", scriptName);

        var offenders = script
            .Select((character, index) => (Character: character, Index: index))
            .Where(x => x.Character > '\x007F')
            .Take(10)
            .Select(x => $"U+{(int)x.Character:X4} at offset {x.Index}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{scriptName} contains non-ASCII characters: {string.Join(", ", offenders)}. "
            + "Windows PowerShell 5.1 reads this BOM-less file as ANSI, corrupting them and "
            + "breaking the parse. Use '-' instead of an em dash and '...' instead of an "
            + "ellipsis.");
    }

    [Fact]
    public void The_template_documents_what_breaks_when_a_boot_gate_is_missing()
    {
        var template = ReadRepoFile("deploy", TemplateName);

        // Rotating the file-store KEK strands every already-stored file.
        Assert.Contains("undecryptable", template, StringComparison.Ordinal);

        // MeetingLinks:PublicWebBaseUrl empty => the Approve / Resend actions
        // are refused UP FRONT with a bilingual 409, not silently skipped.
        Assert.Contains("MEETING_LINKS_NOT_CONFIGURED", template, StringComparison.Ordinal);

        // The three Production boot gates are flagged as such, and the script
        // names them back to the operator when they are left empty.
        Assert.Contains("REFUSE TO START", template, StringComparison.Ordinal);
    }

    /// <summary>The three per-service scripts were merged on 2026-08-06. They
    /// must not come back: two scripts writing the same Machine-scope variable
    /// is how a box silently takes whichever ran last.</summary>
    [Fact]
    public void The_superseded_per_service_scripts_are_gone()
    {
        foreach (var name in new[]
                 {
                     "set-env-api.template.ps1",
                     "set-env-cp.ps1",
                     "set-env-web.ps1",
                 })
        {
            var path = Path.Combine(RepoRoot(), "deploy", name);
            Assert.False(
                File.Exists(path),
                $"deploy/{name} is back. The three per-service scripts were merged "
                + $"into deploy/{TemplateName}; re-adding one reintroduces the "
                + "duplicate-variable hazard the merge removed.");
        }
    }

    /// <summary>The load-bearing safety property. The filled overlay carries
    /// real production credentials and MUST stay ignored; the tracked, shareable
    /// artefact is the separate template.</summary>
    [Fact]
    public void The_filled_env_scripts_are_still_gitignored()
    {
        var gitignore = ReadRepoFile(".gitignore");

        // The current overlay, and the pre-merge one that may still exist on a
        // server provisioned before 2026-08-06.
        foreach (var ignored in new[] { "deploy/set-env.ps1", "deploy/set-env-api.ps1" })
        {
            Assert.True(
                gitignore.Contains(ignored, StringComparison.Ordinal),
                $"{ignored} must STAY in .gitignore — it is a filled overlay "
                + "holding the production SQL connection string, the SMTP app "
                + $"password and the key literals. To share the variable list, "
                + $"edit deploy/{TemplateName} instead; never un-ignore a filled "
                + "script.");
        }
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

    /// <summary>The runbook must ask for the setting that decides where uploaded
    /// files land, and must not ask for the dead per-asset roots it replaced.</summary>
    [Fact]
    public void The_production_runbook_prompts_for_the_live_storage_setting_only()
    {
        var runbook = ReadRepoFile("deploy", RunbookName);

        Assert.Contains("SIMF_FileStorage__RootPath", runbook, StringComparison.Ordinal);

        foreach (var dead in new[] { "SIMF_Storage__AvatarBase", "SIMF_Storage__UserIdDocumentBase" })
        {
            Assert.False(
                Regex.IsMatch(runbook, $@"Name\s*=\s*""{Regex.Escape(dead)}"""),
                $"deploy/{RunbookName} still prompts an operator for {dead}, which "
                + "configures nothing since D-568 replaced the per-asset stores "
                + "with the unified file store.");
        }
    }

    [Fact]
    public void The_deploy_readme_documents_the_scripts_together()
    {
        var readme = ReadRepoFile("deploy", "README.md");

        foreach (var script in new[] { TemplateName, RunbookName })
        {
            Assert.True(
                readme.Contains(script, StringComparison.Ordinal),
                $"deploy/README.md does not mention {script}.");
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
