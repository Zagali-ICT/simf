// Tests: this file IS the test - a repo-wide ratchet, not a unit test of a type.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// SIMF installs NO certificate-validation override anywhere. Not in the API
/// clients, not in the deploy scripts, not in the mobile app.
///
/// <para>
/// The history is why this is a test and not a convention. The Flutter app
/// shipped a blanket <c>badCertificateCallback =&gt; true</c> for months (finding
/// C2). The Control Panel and Website shipped
/// <c>DangerousAcceptAnyServerCertificateValidator</c> behind a config flag, and
/// on 2026-08-07 that flag was still on while the base address was repointed
/// from loopback to a public origin - which handed the admin password, the TOTP
/// code and the perm:* bearer token to anything that answered for the name. The
/// deploy runbook had its own trust-all around the health probe, which meant the
/// step that verifies a deployment would accept an answer from anywhere.
/// </para>
///
/// <para>
/// Every one of those was added by someone solving a real, immediate problem: a
/// certificate that would not validate. That is exactly the moment this control
/// gets removed again, and it is why the check has to fail the build rather than
/// live in a comment. If a certificate does not validate, fix the certificate.
/// </para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class CertificateValidationBypassTests
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

    // Assignment, not mention: a comment naming the API (this file, and the
    // template's note recording that the setting was deleted) must not trip it,
    // while any code that actually installs an override must.
    private static readonly Regex BypassAssignment = new(
        @"(ServerCertificateCustomValidationCallback|ServerCertificateValidationCallback|RemoteCertificateValidationCallback|badCertificateCallback)\s*(=|=>)",
        RegexOptions.Compiled);

    private static string StripComments(string source, string lineCommentToken)
    {
        var kept = source
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith(lineCommentToken, StringComparison.Ordinal));

        return string.Join("\n", kept);
    }

    private static IEnumerable<string> FilesUnder(string relativeDirectory, string pattern)
    {
        var root = Path.Combine(RepoRoot(), relativeDirectory);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
    }

    [Theory]
    // The server-side clients: these forward the bearer token.
    [InlineData("src", "*.cs", "//")]
    // The operator-run scripts: an unvalidated probe reports a deployment
    // healthy on an answer from anything at all.
    [InlineData("deploy", "*.ps1", "#")]
    // The mobile app: finding C2, deleted 2026-08-07.
    [InlineData("src/Mobile/simf_app/lib", "*.dart", "//")]
    public void No_source_file_installs_a_certificate_validation_override(
        string relativeDirectory, string pattern, string lineCommentToken)
    {
        var files = FilesUnder(relativeDirectory, pattern).ToList();

        // Guards the guard: a wrong path would make this vacuously green.
        Assert.True(
            files.Count > 0,
            $"No {pattern} files found under {relativeDirectory}; the enumeration is wrong.");

        var offenders = files
            .Select(path => (Path: path, Code: StripComments(File.ReadAllText(path), lineCommentToken)))
            .Where(file => BypassAssignment.IsMatch(file.Code))
            .Select(file => Path.GetRelativePath(RepoRoot(), file.Path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These files install a certificate-validation override: "
            + $"{string.Join(", ", offenders)}. SIMF validates certificates everywhere - "
            + "the CP/Website clients carry the admin password, the TOTP code and the "
            + "perm:* bearer token, so anything answering for the host would receive them. "
            + "If a certificate does not validate, fix the certificate; there is "
            + "deliberately no option to skip the check.");
    }

    [Fact]
    public void The_retired_trust_all_setting_is_not_reintroduced()
    {
        // Reading it again would be the first step back: the key is meaningless
        // now, so any new reference means someone is rebuilding the escape hatch.
        var offenders = FilesUnder("src", "*.cs")
            .Concat(FilesUnder("src", "*.json"))
            .Where(path => File.ReadAllText(path)
                .Contains("AllowSelfSignedCertificate", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepoRoot(), path))
            .OrderBy(path => path)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Api:AllowSelfSignedCertificate is referenced again in: {string.Join(", ", offenders)}. "
            + "That setting was deleted on 2026-08-08 along with the handler it controlled.");
    }
}
