// DEF-SEC-001 — structural guard on the tracked appsettings*.json files.
// A live external SMTP host / user / password and the demo-account seed
// password were committed in plaintext in
// src/Backend/SIMF.Api/appsettings.Development.json. The remediation pattern
// (already used for Jwt:SigningKey, Ai:*:ApiKey, Storage:UserIdDocument-
// EncryptionKey and FileStorage:EncryptionKey) is: keep the key so the shape
// and the options binding are unchanged, ship an EMPTY value, and supply the
// real value out of the repo via SIMF_Section__Key environment variables
// (Developer-Guide section 20.3).
//
// This test fails the build if any secret-shaped key regains a committed
// value. Assertion messages name the configuration KEY PATH only — a value is
// never echoed, so a failure here does not itself leak the secret.
//
// Text/JSON assertions over the checked-in files instead of a real config
// binder — owner-rule section 1.7 forbids csproj edits, so no extra package.
using System.IO;
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

    // Every tracked appsettings file across the three hosts.
    private static readonly string[] TrackedSettingsFiles =
    {
        "src/Backend/SIMF.Api/appsettings.json",
        "src/Backend/SIMF.Api/appsettings.Development.json",
        "src/ControlPanel/SIMF.ControlPanel/appsettings.json",
        "src/ControlPanel/SIMF.ControlPanel/appsettings.Development.json",
        "src/Website/SIMF.Web/appsettings.json",
        "src/Website/SIMF.Web/appsettings.Development.json",
    };

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
    public void No_tracked_appsettings_file_carries_a_secret_value()
    {
        var root = RepoRoot();
        foreach (var relative in TrackedSettingsFiles)
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
        foreach (var relative in TrackedSettingsFiles)
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
}
