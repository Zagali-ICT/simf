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
