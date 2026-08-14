// Tests: SIMF.Api.Tests/DeploymentEnvPrefixTests.cs
//        (detects a legacy variable, ignores the un-prefixed and the current
//         forms, and names the script that fixes it.)
namespace SIMF.Common;

/// <summary>Refuses to start a host whose server still carries the pre-split
/// <c>SIMF_</c> environment variables.
///
/// <para>Until 2026-08-12 every SIMF host read one shared prefix, <c>SIMF_</c>.
/// Machine scope is shared by every process on a box, so two SIMF applications
/// on one server could not be given different values for the same key: there
/// was one <c>SIMF_Storage__LogDirectory</c> and one <c>SIMF_Api__BaseUrl</c>
/// between them, shared whether that was wanted or not. Each host now reads its
/// own prefix, so the collision cannot arise.</para>
///
/// <para>The upgrade is not backward compatible, and deliberately so: honouring
/// the old names as a fallback would keep the collision alive on exactly the
/// shared box the change exists to fix. That leaves one failure worth catching.
/// A server provisioned before the change still holds the old variables, this
/// build does not read them, and the symptom is a host reporting that an
/// encryption key or connection string is missing while the value sits in the
/// machine environment under its former name. An operator reasonably concludes
/// the deployment is broken rather than that it is half-upgraded.</para>
///
/// <para>So it is named rather than guessed at: the guard lists what it found
/// and which script re-provisions it.</para></summary>
public static class SimfLegacyEnvironmentGuard
{
    private const string LegacyPrefix = "SIMF_";

    /// <summary>Throws when the machine carries <c>SIMF_</c> variables that this
    /// host no longer reads.</summary>
    /// <param name="currentPrefix">This application's prefix, e.g. <c>SIMF_API_</c>.</param>
    /// <param name="isProduction">Checked in Production only, matching every other
    /// boot gate here (see <c>EnsureAiPromptHashSecretConfigured</c>). The guard is
    /// about a half-upgraded SERVER; a developer box legitimately carries whatever
    /// its owner has exported, and the test host runs against pinned values of its
    /// own. Firing there would break the suite on any machine that had ever been
    /// provisioned, which is most of them.</param>
    public static void Verify(string currentPrefix, bool isProduction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPrefix);

        if (!isProduction)
        {
            return;
        }

        var legacy = FindLegacyNames(
            Environment.GetEnvironmentVariables().Keys.Cast<object>()
                .Select(key => key?.ToString())
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!),
            currentPrefix);

        if (legacy.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(BuildMessage(legacy, currentPrefix));
    }

    /// <summary>The failure text. Separated from <see cref="Verify"/> so it can be
    /// asserted without planting machine-scope variables on the build agent, and
    /// because the wording is the whole value of the guard: an operator reading it
    /// has to learn what was found and which script fixes it.</summary>
    internal static string BuildMessage(IReadOnlyList<string> legacy, string currentPrefix) =>
        $"This server still carries {legacy.Count} environment variable(s) using the "
        + $"retired '{LegacyPrefix}' prefix, which this build does not read: "
        + string.Join(", ", legacy.Take(8))
        + (legacy.Count > 8 ? ", ..." : string.Empty)
        + $". Each SIMF application now reads its own prefix ('{currentPrefix}' here) so "
        + "that two applications on one server can hold different values for the same "
        + "key. Re-provision this server with its deploy/set-env-{api|cp|web|edge}.ps1 "
        + "and remove the old variables with deploy/clear-env.ps1, then restart the pool.";

    /// <summary>The legacy names among <paramref name="environmentNames"/>.
    /// Separated from the environment read so it can be tested without setting
    /// machine-scope variables on the build agent.</summary>
    internal static List<string> FindLegacyNames(
        IEnumerable<string> environmentNames, string currentPrefix)
    {
        var known = new[] { "SIMF_API_", "SIMF_CP_", "SIMF_WEB_", "SIMF_EDGE_" };

        return environmentNames
            .Where(name => name.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            // A current per-application name also starts with "SIMF_", so the
            // four live prefixes are excluded before anything is reported. Ours
            // is checked explicitly too, in case the set above ever drifts.
            .Where(name => !name.StartsWith(currentPrefix, StringComparison.Ordinal))
            .Where(name => !known.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            // Test-harness and tooling variables are not application config and
            // are not re-provisioned by set-env; flagging them would make the
            // guard fire on a developer box for no reason.
            .Where(name => !name.StartsWith("SIMF_TEST_", StringComparison.Ordinal))
            .Where(name => !name.StartsWith("SIMF_SMOKE_", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
