// DEF-SEC-001 — structural guard on committed secrets.
//
// Round 1 blanked a live external SMTP host / user / password and the demo-
// account seed password in src/Backend/SIMF.Api/appsettings.Development.json.
// The remediation pattern (already used for Jwt:SigningKey, Ai:*:ApiKey,
// Storage:UserIdDocumentEncryptionKey and FileStorage:EncryptionKey) is: keep
// the key so the shape and the options binding are unchanged, ship an EMPTY
// value, and supply the real value out of the repo via SIMF_Section__Key
// environment variables (Developer-Guide section 20.3).
//
// Round 2 widened the guard after the review found the remediation was partial:
//   * the tracked appsettings files are now DISCOVERED, not hardcoded, so a
//     newly added host project cannot slip past the scan;
//   * a second scan covers EVERY tracked text file, not just the JSON, because
//     the values that were blanked in config were still committed in a manual,
//     a decisions-log row and the integration-test fixture.
//
// The forbidden values are matched by SHA-256 + length + a rolling additive
// checksum, so this file itself commits no secret and no greppable fragment of
// one. Assertion messages name the configuration KEY PATH and the FILE PATH
// only — a value is never echoed, so a failure here does not leak the secret.
//
// Text/JSON assertions over the checked-in files instead of a real config
// binder — owner-rule section 1.7 forbids csproj edits, so no extra package.
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SIMF.Common.Options;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class CommittedSecretsTests
{
    // The appsettings files are checked into the repo. The test project runs
    // from a deep bin directory at test time, so walk upward to the repo root.
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

    // ---------------------------------------------------------------------
    // Tracked-file discovery
    // ---------------------------------------------------------------------

    private static readonly Lazy<IReadOnlyList<string>> TrackedFiles =
        new(DiscoverTrackedFiles);

    /// <summary>Repo-relative, forward-slash paths of every tracked file.
    /// <c>git ls-files</c> is authoritative (an untracked scratch file must not
    /// fail the build); a source drop without git falls back to walking the
    /// source directories.</summary>
    private static IReadOnlyList<string> DiscoverTrackedFiles()
    {
        var root = RepoRoot();
        var fromGit = TryGitLsFiles(root);
        return fromGit.Count > 0 ? fromGit : EnumerateSourceDirectories(root);
    }

    private static List<string> TryGitLsFiles(string root)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("ls-files");
            startInfo.ArgumentList.Add("-z");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new List<string>();
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(120_000);
            if (process.ExitCode != 0)
            {
                return new List<string>();
            }

            return output
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
        catch (Exception)
        {
            // git missing, or not a working tree — the caller falls back.
            return new List<string>();
        }
    }

    private static readonly string[] FallbackScanRoots =
    {
        "src", "tests", "docs", "tools", "deploy",
    };

    private static readonly string[] ExcludedPathSegments =
    {
        "/bin/", "/obj/", "/.git/", "/node_modules/", "/.dart_tool/",
        "/build/", "/TestResults/",
    };

    private static List<string> EnumerateSourceDirectories(string root)
    {
        var results = new List<string>();
        foreach (var scanRoot in FallbackScanRoots)
        {
            var directory = Path.Combine(root, scanRoot);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(
                         directory, "*", SearchOption.AllDirectories))
            {
                var relative = Path
                    .GetRelativePath(root, file)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (ExcludedPathSegments.Any(segment => ("/" + relative).Contains(segment, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                results.Add(relative);
            }
        }

        return results;
    }

    /// <summary>Every tracked <c>appsettings*.json</c>, discovered rather than
    /// listed, so a new host project's settings are covered automatically.</summary>
    private static List<string> TrackedSettingsFiles() =>
        TrackedFiles.Value
            .Where(path =>
                Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    // Colon-separated configuration key paths that must never carry a
    // committed value. A key that is absent from a given file is fine — only
    // a present-and-non-empty value fails.
    private static readonly string[] SecretKeyPaths =
    {
        "Email:Host",
        "Email:User",
        "Email:Password",
        "Seed:DemoPassword",
        "Jwt:SigningKey",
        "SuperAdmin:TempPassword",
        "SuperAdmin:TotpSecret",
        "Storage:UserIdDocumentEncryptionKey",
        "FileStorage:EncryptionKey",
        "Swagger:Password",
        "Ai:Gemini:ApiKey",
        "Ai:Anthropic:ApiKey",
        "Ai:OpenAi:ApiKey",
    };

    // Inline-credential markers in a connection string. Local development uses
    // Trusted_Connection, so a committed User Id / Password pair is a leak.
    private static readonly string[] ConnectionStringCredentialMarkers =
    {
        "password=",
        "pwd=",
        "user id=",
        "uid=",
    };

    [Fact]
    public void Tracked_appsettings_discovery_finds_every_host()
    {
        var discovered = TrackedSettingsFiles();

        // Sanity: a broken discovery must fail loudly rather than scan nothing.
        foreach (var expected in new[]
                 {
                     "src/Backend/SIMF.Api/appsettings.json",
                     "src/Backend/SIMF.Api/appsettings.Development.json",
                     "src/ControlPanel/SIMF.ControlPanel/appsettings.json",
                     "src/Website/SIMF.Web/appsettings.json",
                 })
        {
            Assert.Contains(expected, discovered);
        }
    }

    [Fact]
    public void No_tracked_appsettings_file_carries_a_secret_value()
    {
        var root = RepoRoot();
        foreach (var relative in TrackedSettingsFiles())
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Tracked settings file not found: {relative}");

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var keyPath in SecretKeyPaths)
            {
                if (!TryReadString(document.RootElement, keyPath, out var value))
                {
                    continue;
                }

                Assert.True(
                    string.IsNullOrEmpty(value),
                    $"DEF-SEC-001 — {relative} commits a value for '{keyPath}'. "
                    + "Blank it (keep the key) and supply it via the "
                    + $"SIMF_{keyPath.Replace(":", "__")} environment variable "
                    + "(Developer-Guide section 20.3).");
            }
        }
    }

    [Fact]
    public void No_tracked_connection_string_carries_an_inline_credential()
    {
        var root = RepoRoot();
        foreach (var relative in TrackedSettingsFiles())
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("ConnectionStrings", out var section)
                || section.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in section.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var lowered = (entry.Value.GetString() ?? string.Empty).ToLowerInvariant();
                foreach (var marker in ConnectionStringCredentialMarkers)
                {
                    Assert.False(
                        lowered.Contains(marker),
                        $"DEF-SEC-001 — {relative} commits an inline SQL credential in "
                        + $"'ConnectionStrings:{entry.Name}'. Use Trusted_Connection locally, "
                        + $"or supply the string via SIMF_ConnectionStrings__{entry.Name}.");
                }
            }
        }
    }

    private static bool TryReadString(JsonElement root, string keyPath, out string? value)
    {
        value = null;
        var current = root;
        foreach (var segment in keyPath.Split(':'))
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(segment, out var child))
            {
                return false;
            }

            current = child;
        }

        if (current.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = current.GetString();
        return true;
    }

    [Fact]
    public void Secret_keys_survive_blanking_so_the_options_binding_is_unchanged()
    {
        // The fix must blank the VALUE, never delete the key — deleting it
        // would silently change the configuration shape the Options classes
        // bind against. Assert the Email + Seed keys are still present in the
        // API Development file.
        var path = Path.Combine(
            RepoRoot(),
            "src", "Backend", "SIMF.Api", "appsettings.Development.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var required = new[]
        {
            "Email:Host",
            "Email:User",
            "Email:Password",
            "Seed:DemoPassword",
        };

        foreach (var keyPath in required)
        {
            Assert.True(
                TryReadString(document.RootElement, keyPath, out _),
                $"DEF-SEC-001 — '{keyPath}' must stay present (empty) in "
                + "appsettings.Development.json so the options binding is unchanged.");
        }
    }

    [Fact]
    public void Api_development_configuration_still_binds_and_env_overrides_win()
    {
        // The API layers appsettings.json -> appsettings.Development.json ->
        // AddEnvironmentVariables("SIMF_") (Program.cs). Prove the blanked
        // values still bind to a well-formed options object, and that the
        // documented SIMF_Email__Password / SIMF_Seed__DemoPassword overrides
        // reach the same keys — that is what a developer uses locally now.
        var apiDirectory = Path.Combine(RepoRoot(), "src", "Backend", "SIMF.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        Assert.NotNull(email);
        Assert.Equal(string.Empty, email!.Host);
        Assert.Equal(string.Empty, email.User);
        Assert.Equal(string.Empty, email.Password);
        Assert.Equal(587, email.Port);
        Assert.Equal("SIMF", email.FromName);

        var seed = configuration.GetSection(DemoSeedOptions.SectionName).Get<DemoSeedOptions>();
        Assert.NotNull(seed);
        Assert.Equal(string.Empty, seed!.DemoPassword);

        // Same layering with the SIMF_-prefixed environment source on top.
        var overridden = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Host"] = "localhost",
                ["Email:User"] = "dev@simf.example",
                ["Email:Password"] = "dev-only-not-a-real-secret",
                ["Seed:DemoPassword"] = "Dev@Local1!",
            })
            .Build();

        var overriddenEmail = overridden.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        Assert.Equal("localhost", overriddenEmail!.Host);
        Assert.Equal("dev@simf.example", overriddenEmail.User);
        Assert.Equal("dev-only-not-a-real-secret", overriddenEmail.Password);
        Assert.Equal(
            "Dev@Local1!",
            overridden.GetSection(DemoSeedOptions.SectionName).Get<DemoSeedOptions>()!.DemoPassword);
    }

    // ---------------------------------------------------------------------
    // Literal scan over every tracked text file
    // ---------------------------------------------------------------------

    /// <summary>A credential that must not appear in a tracked file, identified
    /// by a one-way fingerprint so this test commits no secret.
    /// <paramref name="KnownRemainingPaths"/> is the documented residual
    /// inventory: places the value is deliberately (or not yet) retained. Each
    /// entry carries a reason in the declaration below and is itself asserted
    /// to be still accurate, so the list can only shrink.</summary>
    private sealed record CredentialFingerprint(
        string Label,
        int Length,
        int CharSum,
        string Sha256Hex,
        string[] KnownRemainingPaths);

    private static readonly CredentialFingerprint[] ForbiddenCredentials =
    {
        // Seed:DemoPassword — the D-585 demo-account shared password. Blanked
        // in config (round 1) and removed from the fixture + the two docs
        // (round 2), so nothing may carry it any more.
        new(
            "Seed:DemoPassword (supply SIMF_Seed__DemoPassword)",
            14,
            1089,
            "7954abf373c465906bd6a4883954f6fdefecd35fefe593693520d2ff452915a8",
            Array.Empty<string>()),

        // SuperAdmin:TempPassword — the bootstrap super-admin seed password.
        // Removed from the Developer-Guide port map; the residual occurrences
        // are the boot-time deny-list and the dev-login line repeated across
        // the E2E catalogue + the smoke script. Owner ops: rotate the
        // credential, then strip these and shrink this list.
        //
        // 2026-07-27 (owner decision, "fix docs"): the three NARRATIVE
        // documents that merely recorded the finding were redacted — they now
        // name the configuration key path instead of the value — so their
        // entries are gone from this list and the guard protects them from
        // here on. Redaction is not rotation: the value is still in git
        // history, so the rotation item stays open.
        new(
            "SuperAdmin:TempPassword (supply SIMF_SuperAdmin__TempPassword)",
            12,
            703,
            "59e64b4df89c9e379261d867524a8d17a911dce7e763373f2efb991d292db8eb",
            new[]
            {
                // Refuses to boot in Production when the value is still this one.
                "src/Backend/SIMF.Api/Program.cs",
                // E2E catalogue + smoke script sign-in lines — these are
                // executable fixtures (the literal is the input a runner
                // types), not prose, so they are cleared by the rotation op.
                "docs/tests/e2e/cp-account-profile.md",
                "docs/tests/e2e/cp-admin-admins.md",
                "docs/tests/e2e/cp-admin-archive.md",
                "docs/tests/e2e/cp-admin-bookings.md",
                "docs/tests/e2e/cp-admin-halls.md",
                "docs/tests/e2e/cp-admin-interests.md",
                "docs/tests/e2e/cp-auth-flow.md",
                "docs/tests/e2e/web-login.md",
                "tools/smoke/smoke.sh",
            }),

        // SuperAdmin:TotpSecret — the development TOTP seed, in its two written
        // forms (grouped with spaces, and tight). Removed from the
        // Developer-Guide port map; residual occurrences are the myComment #35
        // regression fixture and the same documentation set as above. (The FDS
        // and the security assessment quote a TRUNCATED prefix only, so they
        // are not listed — the scan does not match them.)
        new(
            "SuperAdmin:TotpSecret, spaced form (supply SIMF_SuperAdmin__TotpSecret)",
            39,
            3435,
            "69597ef04e809968ab93708d3d0bc52cf9efb5f1c45f7d2e2943d40c7e5485ee",
            new[]
            {
                "tests/SIMF.Api.Tests/TotpVerifierTests.cs",
                "docs/SIMF-Follow-Up-Backlog.md",
                "docs/decisions/DECISIONS_LOG.md",
                "docs/manuals/Test-Guide.md",
                "docs/tests/e2e/cp-admin-interests.md",
                "docs/tests/e2e/cp-auth-flow.md",
                "tools/smoke/smoke.sh",
            }),
        new(
            "SuperAdmin:TotpSecret, unspaced form (supply SIMF_SuperAdmin__TotpSecret)",
            32,
            3211,
            "9b5f1475136da99fd65342d283f458320b45f49643bdcbc26cecbdff1c389b81",
            new[]
            {
                "tests/SIMF.Api.Tests/TotpVerifierTests.cs",
            }),
    };

    private static readonly HashSet<string> ScannedTextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".cs", ".razor", ".md", ".txt", ".ps1", ".sh", ".yml",
            ".yaml", ".config", ".xml", ".props", ".targets", ".csproj",
            ".slnx", ".sql", ".dart", ".js", ".css", ".html", ".http",
            ".cmd", ".bat", ".env",
        };

    private const long MaxScannedFileBytes = 8L * 1024 * 1024;

    [Fact]
    public void No_tracked_file_carries_a_known_committed_credential()
    {
        var root = RepoRoot();
        var tracked = TrackedFiles.Value;
        Assert.True(
            tracked.Count > 100,
            "DEF-SEC-001 — tracked-file discovery returned "
            + $"{tracked.Count} files, which cannot be right; the scan would "
            + "silently pass. Check that 'git ls-files' runs from the repo root.");

        var offenders = new List<string>();
        foreach (var relative in tracked)
        {
            if (!ScannedTextExtensions.Contains(Path.GetExtension(relative)))
            {
                continue;
            }

            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || new FileInfo(path).Length > MaxScannedFileBytes)
            {
                continue;
            }

            var text = File.ReadAllText(path);
            foreach (var credential in ForbiddenCredentials)
            {
                if (credential.KnownRemainingPaths.Contains(relative, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ContainsCredential(text, credential))
                {
                    offenders.Add($"  {relative} -> {credential.Label}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "DEF-SEC-001 — a known committed credential appears in tracked "
            + "file(s):" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders) + Environment.NewLine
            + "Replace the value with the SIMF_* environment-variable key path "
            + "(Developer-Guide section 20.3). If the occurrence is deliberate "
            + "(a boot-time deny-list, or a security finding on record), add the "
            + "path to CredentialFingerprint.KnownRemainingPaths with a reason.");
    }

    [Fact]
    public void The_residual_credential_inventory_is_accurate()
    {
        // The allow-list must not become a hiding place: every path in it has
        // to STILL carry the credential. Once a file is cleaned the entry must
        // go, so the inventory only ever shrinks.
        var root = RepoRoot();
        var stale = new List<string>();

        foreach (var credential in ForbiddenCredentials)
        {
            foreach (var relative in credential.KnownRemainingPaths)
            {
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) || !ContainsCredential(File.ReadAllText(path), credential))
                {
                    stale.Add($"  {relative} -> {credential.Label}");
                }
            }
        }

        Assert.True(
            stale.Count == 0,
            "DEF-SEC-001 — the residual credential inventory is stale; these "
            + "paths no longer carry the credential (or no longer exist):"
            + Environment.NewLine + string.Join(Environment.NewLine, stale)
            + Environment.NewLine
            + "Remove the entries from CredentialFingerprint.KnownRemainingPaths.");
    }

    /// <summary>Sliding-window substring search that never holds the value:
    /// a cheap additive char checksum pre-filters, and SHA-256 confirms.</summary>
    private static bool ContainsCredential(string text, CredentialFingerprint credential)
    {
        var length = credential.Length;
        if (text.Length < length)
        {
            return false;
        }

        var checksum = 0;
        for (var i = 0; i < length; i++)
        {
            checksum += text[i];
        }

        for (var start = 0; ; start++)
        {
            if (checksum == credential.CharSum
                && MatchesHash(text, start, length, credential.Sha256Hex))
            {
                return true;
            }

            var next = start + length;
            if (next >= text.Length)
            {
                return false;
            }

            checksum += text[next] - text[start];
        }
    }

    private static bool MatchesHash(string text, int start, int length, string expectedHex)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(text.Substring(start, length)), hash);
        return Convert.ToHexString(hash).Equals(expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Developer-Guide section 20.3 / 20.4 accuracy
    // ---------------------------------------------------------------------

    [Fact]
    public void The_developer_guide_describes_the_real_empty_key_behaviour()
    {
        // The guide claimed the AI provider "falls back to Echo" with the keys
        // empty. It does not: appsettings.json ships DefaultProvider=Echo, but
        // the API's Development file overrides it to a real provider with an
        // empty key, and AiProviderRouting redirects Echo-default prompts to
        // it — so AI calls fail with 503 AI_PROVIDER_NOT_CONFIGURED.
        var apiDirectory = Path.Combine(RepoRoot(), "src", "Backend", "SIMF.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var defaultProvider = configuration["Ai:DefaultProvider"];
        Assert.False(
            string.Equals(defaultProvider, "Echo", StringComparison.OrdinalIgnoreCase),
            "The API Development config no longer overrides Ai:DefaultProvider "
            + "away from Echo — re-check the Developer-Guide wording.");
        Assert.True(string.IsNullOrEmpty(configuration[$"Ai:{defaultProvider}:ApiKey"]));

        var guide = File.ReadAllText(Path.Combine(
            RepoRoot(), "docs", "manuals", "Developer-Guide.md"));

        Assert.DoesNotContain("falls back to `Echo`", guide, StringComparison.Ordinal);
        Assert.Contains("AI_PROVIDER_NOT_CONFIGURED", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void The_developer_guide_port_map_points_at_the_environment_variables()
    {
        // Section 20.4 used to print a working super-admin login and the dev
        // TOTP secret. It must name the SIMF_* key paths instead.
        var guide = File.ReadAllText(Path.Combine(
            RepoRoot(), "docs", "manuals", "Developer-Guide.md"));

        Assert.Contains("SIMF_SuperAdmin__TempPassword", guide, StringComparison.Ordinal);
        Assert.Contains("SIMF_SuperAdmin__TotpSecret", guide, StringComparison.Ordinal);
    }
}
