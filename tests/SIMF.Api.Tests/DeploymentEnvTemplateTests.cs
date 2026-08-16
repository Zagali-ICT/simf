// Guards on the deployment environment scripts + the controlled documents that
// describe them.
//
// Context: an earlier attempt to make the env script shareable tried to DELETE
// its .gitignore entry, which would have committed a live SQL connection
// string, an SMTP app password and several production key literals. The safe
// shape is a separate, differently-named TEMPLATE, while the filled overlay
// stays ignored. These tests pin that shape so the dangerous version cannot
// come back:
//
//   * deploy/set-env-{api,cp,web,edge}.ps1 are tracked and every
//     SECRET value is EMPTY;
//   * .gitignore STILL ignores the filled overlays;
//   * deploy/configure-prod-env.ps1 never overwrites an existing encryption key.
//
// History, because the file count has now moved twice and the reasoning differs
// each time. Until 2026-08-06 there were three per-service scripts; they were
// merged into one because all three wrote to the SAME Machine-scope namespace on
// the SAME box and overlapped on several keys, each noting "running both is
// fine, the last writer wins" - true only while the copies agree. On 2026-08-12
// the estate moved to one server per package, which removes that collision
// outright: a variable set on the Website host is not visible on the API host,
// so there is no last writer. The scripts split again, one per package, and the
// each application then took its OWN environment prefix (SIMF_API_, SIMF_CP_,
// SIMF_WEB_, SIMF_EDGE_), so two SIMF apps on one box can hold different values
// for the same key instead of sharing one. Every_variable_carries_its_own_package_prefix
// pins that, and The_blazor_hosts_agree_on_the_settings_that_must_match pins the
// two settings that still have to match.
//
// The bright line stays where the merge put it: "every SECRET is empty", not
// "every value is empty". An all-empty template meant hand-filling ~60
// variables, which is not a deployment step anybody performs reliably. It stays
// mechanically checkable because each entry carries its own Secret = $true/$false
// flag rather than leaving the reader to judge which is which.
//
// Text assertions over the checked-in files (no PowerShell is executed and the
// runbook is never run) - owner-rule section 1.7 forbids csproj edits, so no
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

    /// <summary>One script per deployment package.</summary>
    public static TheoryData<string> Templates() => new()
    {
        "set-env-api.ps1",
        "set-env-cp.ps1",
        "set-env-web.ps1",
        "set-env-edge.ps1",
    };

    private static readonly string[] TemplateNames =
    {
        "set-env-api.ps1",
        "set-env-cp.ps1",
        "set-env-web.ps1",
        "set-env-edge.ps1",
    };

    // Matches one entry of a $vars array:
    //   [pscustomobject]@{ Name = "X"; Value = "Y"; Secret = $false; Gate = $false; ... }
    //
    // BOTH QUOTE STYLES. The files mix them - Value = 'Anthropic' sits beside
    // Value = "false" - and a double-quote-only pattern silently drops every
    // single-quoted entry. That does not fail here; it fails as "missing the
    // required key X" in a different test, which is a long way from the cause.
    // .NET allows the same group name on both alternatives.
    private static readonly Regex EntryPattern =
        new("""Name\s*=\s*"(?<name>[A-Za-z0-9_]+)"\s*;\s*Value\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')\s*;\s*Secret\s*=\s*\$(?<secret>true|false)\s*;\s*Gate\s*=\s*\$(?<gate>true|false)""",
            RegexOptions.Multiline | RegexOptions.Compiled);

    private static MatchCollection EntriesOf(string templateName) =>
        EntryPattern.Matches(ReadRepoFile("deploy", templateName));

    // -------------------------------------------------------------------
    // E1 - the templates
    // -------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Templates))]
    public void Each_env_template_is_tracked_under_its_own_template_name(string templateName)
    {
        var template = ReadRepoFile("deploy", templateName);

        Assert.Contains("#Requires -RunAsAdministrator", template, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariableTarget]::Machine", template, StringComparison.Ordinal);

        // The "an empty value is SKIPPED with a warning" behaviour, so an
        // unedited template never blanks a working server.
        Assert.Contains("IsNullOrWhiteSpace", template, StringComparison.Ordinal);
        Assert.Contains("SKIP (empty", template, StringComparison.Ordinal);
    }

    /// <summary>
    /// Guards the parser, not the contents.
    ///
    /// <para>This test used to assert that every <c>Secret = $true</c> value
    /// shipped EMPTY, because the tracked file was a template and the filled
    /// overlay was gitignored. That pair is gone: <c>deploy/</c> now holds five
    /// <c>set-env-*.ps1</c> which ARE the operator's filled files, carrying real
    /// connection strings and keys, tracked at the owner's instruction. The
    /// emptiness assertion would fail by design, so it was removed rather than
    /// weakened into something that reads like a guarantee it no longer gives.
    /// </para>
    ///
    /// <para>What survives is the guard-the-guard: a broken regex would make
    /// every other assertion in this file vacuously green, and that failure is
    /// silent. The smallest script (web) declares four entries.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void Every_env_script_parses_its_variable_entries(string templateName)
    {
        var entries = EntriesOf(templateName);

        Assert.True(
            entries.Count >= 4,
            $"Only {entries.Count} variable entries were parsed out of {templateName}; "
            + "the script or the parser is wrong. Check the quote style first - the "
            + "entry pattern accepts both \" and ', and a third style would drop "
            + "entries silently.");
    }

    /// <summary>No template may declare the same variable twice.
    /// <para>
    /// The merged template did: SIMF_ReverseProxy__KnownProxies__0 appeared once
    /// with Gate = $true and again with Gate = $false, so the apply loop set it
    /// twice and the box kept whichever ran last. Within one file that is a
    /// silent contradiction; the old SingleValue helper only checked a handful of
    /// named keys, so it never caught this one.
    /// </para></summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void No_env_template_declares_a_variable_twice(string templateName)
    {
        var duplicates = EntriesOf(templateName)
            .Select(m => m.Groups["name"].Value)
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} (x{group.Count()})")
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"{templateName} declares the same variable more than once: "
            + string.Join(", ", duplicates)
            + ". The apply loop would set it twice and the server would keep the "
            + "last one, which is a contradiction no reader can see.");
    }

    /// <summary>The prefix each package's variables must carry.</summary>
    private static string PrefixFor(string templateName) => templateName switch
    {
        "set-env-api.ps1" => "SIMF_API_",
        "set-env-cp.ps1" => "SIMF_CP_",
        "set-env-web.ps1" => "SIMF_WEB_",
        "set-env-edge.ps1" => "SIMF_EDGE_",
        _ => throw new ArgumentOutOfRangeException(nameof(templateName), templateName, null),
    };

    private static string StripPrefix(string name)
    {
        foreach (var prefix in new[] { "SIMF_EDGE_", "SIMF_WEB_", "SIMF_API_", "SIMF_CP_" })
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }
        return name;
    }

    /// <summary>THE isolation guarantee. Every variable a template declares must
    /// carry that package's own prefix.
    /// <para>
    /// Machine scope is shared by every process on a box. While all four hosts
    /// read one common <c>SIMF_</c>, two SIMF applications on one server could not
    /// be given different values for the same key: there was a single
    /// <c>SIMF_Storage__LogDirectory</c> and a single <c>SIMF_Api__BaseUrl</c>
    /// between them, shared whether that was wanted or not. Each host now reads
    /// its own prefix, and this is what stops a template quietly reaching back
    /// into the shared namespace.
    /// </para>
    /// <para>
    /// ASPNETCORE_ENVIRONMENT is the one exception and is deliberately NOT
    /// prefixed: the host reads it before any configuration source loads, so a
    /// prefixed form would never be seen.
    /// </para></summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void Every_variable_carries_its_own_package_prefix(string templateName)
    {
        var prefix = PrefixFor(templateName);

        var wrong = EntriesOf(templateName)
            .Select(m => m.Groups["name"].Value)
            .Where(name => name != "ASPNETCORE_ENVIRONMENT")
            .Where(name => !name.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            wrong.Count == 0,
            $"{templateName} declares variable(s) that do not carry its own "
            + $"'{prefix}' prefix: {string.Join(", ", wrong)}. A name outside the "
            + "package's namespace is either read by nobody, or collides with "
            + "another SIMF application sharing the server.");
    }

    /// <summary>Every secret the deployment needs must be declared, even though
    /// it ships empty; an undeclared secret is one an operator never sets. They
    /// all belong to the API, the only package holding credentials.</summary>
    [Fact]
    public void The_api_template_declares_the_known_secrets_as_secret()
    {
        var secrets = EntriesOf("set-env-api.ps1")
            .Where(m => m.Groups["secret"].Value == "true")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in new[]
                 {
                     "ConnectionStrings__SimfIdentityDb",
                     "ConnectionStrings__SimfAppDb",
                     "Jwt__SigningKey",
                     "FileStorage__EncryptionKey",
                     "Storage__UserIdDocumentEncryptionKey",
                     "Ai__PromptHash__Secret",
                     "SuperAdmin__TempPassword",
                     "SuperAdmin__TotpSecret",
                     "Email__Password",
                     "Swagger__Password",
                     "WalkInMode__BadgeKey",
                 })
        {
            Assert.True(
                secrets.Contains("SIMF_API_" + key),
                $"set-env-api.ps1 must declare SIMF_API_{key} with "
                + "Secret = $true, so the emptiness guard covers it.");
        }
    }

    /// <summary>Each package's template carries the keys that package reads.
    /// <para>
    /// The split's failure mode is a key that lands in no template at all, which
    /// nothing else would notice: the server simply comes up with a setting
    /// unset. Named UN-prefixed and checked against the package's own prefix, so
    /// the list says what the setting IS rather than repeating the namespace.
    /// </para></summary>
    [Theory]
    [InlineData("set-env-api.ps1", new[]
    {
        "ConnectionStrings__SimfIdentityDb",
        "ConnectionStrings__SimfAppDb",
        "Jwt__SigningKey",
        "FileStorage__EncryptionKey",
        "FileStorage__RootPath",
        "Storage__UserIdDocumentEncryptionKey",
        "Ai__PromptHash__Secret",
        "MeetingLinks__PublicWebBaseUrl",
        "SuperAdmin__TempPassword",
        "Seed__DemoPassword",
        "Ai__Gemini__ApiKey",
        "Ai__Anthropic__ApiKey",
        "Ai__OpenAi__ApiKey",
        "Storage__LogDirectory",
    })]
    [InlineData("set-env-cp.ps1", new[]
    {
        "Storage__LogDirectory",
        "Api__BaseUrl",
        "Session__LifetimeHours",
        "DataProtection__KeyRingPath",
    })]
    [InlineData("set-env-web.ps1", new[]
    {
        "Storage__LogDirectory",
        "Api__BaseUrl",
        "DataProtection__KeyRingPath",
    })]
    [InlineData("set-env-edge.ps1", new[]
    {
        "Storage__LogDirectory",
        "ReverseProxy__Clusters__api__Destinations__primary__Address",
        "ReverseProxy__KnownProxies__0",
    })]
    public void Each_template_covers_the_keys_its_package_reads(
        string templateName, string[] requiredKeys)
    {
        var prefix = PrefixFor(templateName);
        var declared = EntriesOf(templateName)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Host-level, never prefixed, and every host needs it.
        Assert.Contains("ASPNETCORE_ENVIRONMENT", declared);

        foreach (var key in requiredKeys)
        {
            Assert.True(
                declared.Contains(prefix + key),
                $"{templateName} is missing the required key {prefix}{key}.");
        }
    }

    /// <summary>A package's template must not carry another package's settings.
    /// <para>
    /// This is the whole point of splitting per server: the Website host has no
    /// reason to hold a database connection string, and a file that carries one
    /// puts that credential on a box that never reads it. Checked on the
    /// UN-prefixed key, so it catches the setting whichever namespace it wears.
    /// </para></summary>
    [Theory]
    [InlineData("set-env-cp.ps1", "ConnectionStrings__SimfAppDb")]
    [InlineData("set-env-cp.ps1", "Jwt__SigningKey")]
    [InlineData("set-env-cp.ps1", "Email__Password")]
    [InlineData("set-env-web.ps1", "ConnectionStrings__SimfAppDb")]
    [InlineData("set-env-web.ps1", "FileStorage__EncryptionKey")]
    [InlineData("set-env-web.ps1", "SuperAdmin__TempPassword")]
    [InlineData("set-env-edge.ps1", "ConnectionStrings__SimfAppDb")]
    [InlineData("set-env-edge.ps1", "Jwt__SigningKey")]
    [InlineData("set-env-edge.ps1", "FileStorage__EncryptionKey")]
    [InlineData("set-env-api.ps1", "DataProtection__KeyRingPath")]
    public void A_template_does_not_carry_another_packages_setting(
        string templateName, string foreignKey)
    {
        var suffixes = EntriesOf(templateName)
            .Select(m => StripPrefix(m.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.False(
            suffixes.Contains(foreignKey),
            $"{templateName} declares {foreignKey}, which that server does not read. "
            + "Per-package templates exist so a box holds only what it needs; "
            + "carrying a foreign setting either spreads a credential or has an "
            + "operator configure something that does nothing.");
    }

    /// <summary>Two settings must hold the SAME value in the Control Panel's and
    /// the Website's templates.
    /// <para>
    /// Per-application prefixes mean the two hosts CAN differ on any key, which is
    /// the point - they can log to different directories on one box. But
    /// <c>Api__BaseUrl</c> must match because both talk to the same API, and
    /// <c>DataProtection__KeyRingPath</c> must match because they share ONE ring:
    /// a cookie minted by the Control Panel is decrypted by the Website, and two
    /// rings mean two mutually unreadable cookie sets. Compared on the part after
    /// the prefix, since the names themselves now differ by design.
    /// </para>
    /// <para>
    /// <c>Storage__LogDirectory</c> is deliberately NOT checked: it may
    /// legitimately differ per application, and asserting agreement would
    /// re-impose the shared namespace this change removed.
    /// </para></summary>
    [Theory]
    [InlineData("Api__BaseUrl")]
    [InlineData("DataProtection__KeyRingPath")]
    public void The_blazor_hosts_agree_on_the_settings_that_must_match(string key)
    {
        var found = new[] { "set-env-cp.ps1", "set-env-web.ps1" }
            .Select(template => new
            {
                Template = template,
                Entry = EntriesOf(template)
                    .FirstOrDefault(m => StripPrefix(m.Groups["name"].Value) == key),
            })
            .ToList();

        foreach (var item in found)
        {
            Assert.True(
                item.Entry is not null,
                $"{item.Template} does not declare {key}; both Blazor hosts need it.");
        }

        var distinct = found
            .Select(item => item.Entry!.Groups["value"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            distinct.Count == 1,
            $"The Control Panel and Website templates give {key} different values "
            + $"({string.Join(" vs ", distinct)}). They must match: both hosts talk to "
            + "the same API, and they share one Data Protection key ring - two rings "
            + "means a cookie minted by one is rejected by the other.");
    }

    // Only names that are still genuinely untracked. The five set-env-*.ps1 came
    // OFF this list when the template/overlay pair was collapsed: they are the
    // tracked scripts now, and leaving them here would have excluded the very
    // files this check exists to cover - a scan that silently skips its subject.
    private static readonly string[] UntrackedOverlays =
    {
        "set-env-prod.ps1",
    };

    /// <summary>Windows PowerShell 5.1 decodes a BOM-less file as the ANSI
    /// codepage. An em dash becomes three characters, one of which is a quote -
    /// and inside a string literal that quote ends the string, so the script
    /// stops parsing. The env template carried six and failed with 8 parse
    /// errors under powershell.exe until 2026-08-07 while parsing fine under
    /// pwsh 7, so an operator on Windows Server ran it and set NOTHING.
    /// <para>
    /// A script is safe either way: pure ASCII, or a BOM telling 5.1 to read
    /// UTF-8. Checked across the whole directory rather than a named list, so a
    /// NEW deploy script is covered the day it lands.
    /// </para></summary>
    [Fact]
    public void Every_tracked_deploy_script_decodes_under_windows_powershell()
    {
        var deployDirectory = Path.Combine(RepoRoot(), "deploy");
        var scripts = Directory
            .EnumerateFiles(deployDirectory, "*.ps1", SearchOption.AllDirectories)
            .Where(path => !UntrackedOverlays.Contains(
                Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path)
                .EndsWith(".local.ps1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Guards the guard: a broken glob would make this vacuously green.
        Assert.True(
            scripts.Count >= 5,
            $"Only {scripts.Count} deploy scripts were found under {deployDirectory}; "
            + "the enumeration is wrong.");

        var undecodable = scripts
            .Where(path =>
            {
                var bytes = File.ReadAllBytes(path);
                var hasBom = bytes.Length >= 3
                             && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                return !hasBom && File.ReadAllText(path).Any(character => character > '\x007F');
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(
            undecodable.Count == 0,
            $"BOM-less deploy scripts contain non-ASCII: {string.Join(", ", undecodable)}. "
            + "Windows PowerShell 5.1 reads them as ANSI, which corrupts the characters and "
            + "breaks the parse when one lands inside a string literal. Either use ASCII "
            + "('-' for an em dash, '...' for an ellipsis) or save the file with a UTF-8 BOM.");
    }

    [Fact]
    public void The_api_template_documents_what_breaks_when_a_boot_gate_is_missing()
    {
        var template = ReadRepoFile("deploy", "set-env-api.ps1");

        // Rotating the file-store KEK strands every already-stored file.
        Assert.Contains("undecryptable", template, StringComparison.Ordinal);

        // MeetingLinks:PublicWebBaseUrl empty => the Approve / Resend actions
        // are refused UP FRONT with a bilingual 409, not silently skipped.
        Assert.Contains("MEETING_LINKS_NOT_CONFIGURED", template, StringComparison.Ordinal);

        // The Production boot gates are flagged as such, and the script names
        // them back to the operator when they are left empty.
        Assert.Contains("REFUSE TO START", template, StringComparison.Ordinal);
    }

    /// <summary>The shared Data Protection key ring is a boot gate for the two
    /// Blazor hosts, and their templates have to say so.
    /// <para>
    /// This is the setting whose absence is invisible. Everything else that is
    /// gated fails loudly on one node; a per-node key ring works perfectly on one
    /// node and only breaks when a second is added, and then it presents as admins
    /// being bounced to /login at random rather than as a configuration error. The
    /// tier separation adds those instances, so the gate has to be in place before
    /// the second node exists, not after someone diagnoses the symptom.
    /// </para></summary>
    [Theory]
    [InlineData("set-env-cp.ps1")]
    [InlineData("set-env-web.ps1")]
    public void The_blazor_templates_gate_the_shared_data_protection_key_ring(
        string templateName)
    {
        var entry = EntriesOf(templateName)
            .FirstOrDefault(m => StripPrefix(m.Groups["name"].Value)
                == "DataProtection__KeyRingPath");

        Assert.True(
            entry is not null,
            $"{templateName} must declare {PrefixFor(templateName)}DataProtection__KeyRingPath in the "
            + "standard entry shape, so the operator is given the setting that keeps "
            + "auth cookies and antiforgery tokens valid across instances.");

        Assert.Equal("true", entry!.Groups["gate"].Value);

        // A path is not a credential, and shipping a sensible default filled is
        // what makes the template a usable deployment step.
        Assert.Equal("false", entry.Groups["secret"].Value);

        // The key ring must not be parked under the versioned deploy root. That
        // tree is replaced wholesale by a release, so a ring inside it is
        // destroyed on deploy and every signed-in admin is logged out - the same
        // reasoning that already keeps uploads and logs outside it.
        var value = entry.Groups["value"].Value;
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.DoesNotContain(@"\v1.", value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The API neither reads the key ring nor needs it: bearer tokens,
    /// no antiforgery middleware, no IDataProtector. Declaring it on the API
    /// server would have an operator set a value that configures nothing.</summary>
    [Fact]
    public void The_api_template_does_not_declare_the_key_ring()
    {
        var declared = EntriesOf("set-env-api.ps1")
            .Select(m => StripPrefix(m.Groups["name"].Value))
            .ToList();

        Assert.DoesNotContain("DataProtection__KeyRingPath", declared);
    }

    /// <summary>The mobile edge's forwarding address must be declared, gated and
    /// set - and must not be the edge's own public name.
    /// <para>
    /// It decides where the edge sends every mobile request. This test used to
    /// require it to ship EMPTY, because the file was a template an operator
    /// filled in per site. The file is now the filled script itself, so empty is
    /// the failure rather than the requirement - but the hazard it guarded is
    /// unchanged, so the check inverted instead of being deleted: the plausible
    /// wrong value is `edge.simrsnf.com`, which sends the edge through the load
    /// balancer straight back to itself.
    /// </para></summary>
    [Fact]
    public void The_mobile_edge_forwarding_address_is_set_and_gated()
    {
        var entry = EntriesOf("set-env-edge.ps1")
            .FirstOrDefault(m => StripPrefix(m.Groups["name"].Value)
                == "ReverseProxy__Clusters__api__Destinations__primary__Address");

        Assert.True(
            entry is not null,
            "set-env-edge.ps1 must declare the mobile edge's cluster "
            + "destination, or the edge has nowhere to forward and 502s every app user.");

        Assert.Equal("true", entry!.Groups["gate"].Value);

        var address = entry.Groups["value"].Value;
        Assert.False(
            string.IsNullOrWhiteSpace(address),
            "The edge's forwarding destination is empty, so the edge has nowhere to "
            + "send app traffic and 502s every user. It must point INWARD at the API.");
        Assert.DoesNotContain(
            "edge.", address, StringComparison.OrdinalIgnoreCase);

        // The proxy allowlist is a boot gate here specifically: on the
        // internet-facing hop an unverified X-Forwarded-For lets any caller spoof
        // its source address past the API's rate limiter and into the audit log.
        var proxies = EntriesOf("set-env-edge.ps1")
            .FirstOrDefault(m => StripPrefix(m.Groups["name"].Value)
                == "ReverseProxy__KnownProxies__0");
        Assert.True(proxies is not null,
            "set-env-edge.ps1 must declare ReverseProxy KnownProxies.");
        Assert.Equal("true", proxies!.Groups["gate"].Value);
    }

    /// <summary>The per-package templates exist and the merged one is gone.
    /// <para>
    /// This test used to assert the opposite - that the per-service scripts must
    /// NOT come back - because on a single box two scripts writing the same
    /// Machine-scope variable meant the server kept whichever ran last. Separate
    /// servers remove that hazard: a variable set on one host is not visible on
    /// another, so there is no last writer. What replaces the merge as a guard is
    /// Every_variable_carries_its_own_package_prefix above.
    /// </para></summary>
    [Fact]
    public void The_per_package_templates_exist_and_the_merged_one_is_gone()
    {
        foreach (var name in TemplateNames)
        {
            Assert.True(
                File.Exists(Path.Combine(RepoRoot(), "deploy", name)),
                $"deploy/{name} is missing. Each deployment package runs on its own "
                + "server and needs its own template; without one, that server has no "
                + "documented variable list at all.");
        }

        Assert.False(
            File.Exists(Path.Combine(RepoRoot(), "deploy", "set-env.ps1")),
            "deploy/set-env.ps1 is back. It carried every server's settings "
            + "in one file, which on a per-server estate means shipping the API's "
            + "connection strings and SMTP password to the Website and edge boxes "
            + "that never read them.");
    }

    // `The_filled_env_scripts_are_still_gitignored` stood here. It asserted that
    // deploy/set-env{,-api,-cp,-web,-edge}.ps1 must STAY in .gitignore, because
    // they were filled overlays holding production values while the tracked,
    // shareable artefacts were separate .template.ps1 files.
    //
    // That pair no longer exists. The templates were removed and the five
    // set-env-*.ps1 ARE the tracked operator-facing scripts, carrying the real
    // connection strings and keys, at the owner's instruction. The assertion
    // would now fail by design, so it was deleted rather than softened into
    // something that still reads like a guarantee.
    //
    // `Each_template_is_actually_tracked` below is the surviving half and now
    // carries the whole weight: it asserts the opposite - that no .gitignore
    // pattern matches these five - so the state stays deliberate either way.

    /// <summary>The templates must not themselves be ignored.
    /// <para>
    /// The wildcard that hides the filled overlays is one careless edit away from
    /// hiding the tracked templates too - broadening `set-env-api.ps1` to
    /// `set-env-*.ps1` would swallow every template - and a template nobody can
    /// see is a deployment with no documented variable list. Matched as real
    /// globs rather than as substrings: the file NAMES appear in .gitignore
    /// comments, so a substring check both false-positives on those and misses
    /// the wildcard that would actually hide them.
    /// </para></summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void Each_template_is_actually_tracked(string templateName)
    {
        var path = Path.Combine(RepoRoot(), "deploy", templateName);
        Assert.True(File.Exists(path), $"deploy/{templateName} is missing.");

        var relative = $"deploy/{templateName}";
        var patterns = ReadRepoFile(".gitignore")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        var matching = patterns
            .Where(pattern =>
                System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, relative)
                || System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, templateName))
            .ToList();

        Assert.True(
            matching.Count == 0,
            $"deploy/{templateName} is matched by .gitignore pattern(s) "
            + $"[{string.Join(", ", matching)}]. The templates are the tracked, "
            + "shareable artefacts; only the FILLED overlays may be ignored.");
    }

    // -------------------------------------------------------------------
    // E2 - the runbook
    // -------------------------------------------------------------------
    //
    // Three tests stood here, all reading deploy/configure-prod-env.ps1:
    //   * The_production_runbook_generates_keys_without_overwriting_them
    //   * The_runbook_health_probe_validates_the_certificate
    //   * The_production_runbook_can_be_scoped_to_one_package
    //
    // That file was deleted when deploy/ was reduced to the five set-env-*.ps1
    // plus clean-env.ps1. ReadRepoFile asserts existence, so all three would now
    // fail with "Expected file not found" - a failure about a missing file
    // rather than about anything being wrong.
    //
    // What went with them, recorded so it is a known gap rather than a silent
    // one: nothing now pins the never-overwrite guard on the encryption keys,
    // and rotating FileStorage__EncryptionKey or
    // Storage__UserIdDocumentEncryptionKey makes every stored file and every
    // encrypted PII column undecryptable. Those keys are currently set in
    // set-env-api.ps1 and no script regenerates them, so the hazard needs a
    // person to introduce it - but if key generation is ever added back to a
    // set-env script, the guard and a test for it must come with it.
    //
    // CertificateValidationBypassTests still scans deploy/*.ps1 repo-wide, so
    // the certificate half of the second test is not lost.
}
