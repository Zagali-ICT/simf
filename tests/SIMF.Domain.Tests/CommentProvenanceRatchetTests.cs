using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// Guards the comment standard: source comments explain why the code is shaped as
/// it is, and decision provenance lives in <c>docs/decisions/DECISIONS_LOG.md</c>
/// rather than being copied into the code, where it rots because the log is
/// maintained and the comment is not.
///
/// <para><c>SIMF.Domain</c> was swept to zero and is pinned there. The other layers
/// carry a recorded count that may only go <b>down</b>: the sweep is incremental, so
/// this turns "we will get to it" into a number that cannot drift upward unnoticed
/// and cannot be quietly declared finished either. A prose rule cannot detect the
/// next token added; this can.</para>
/// </summary>
public sealed class CommentProvenanceRatchetTests
{
    // A decision id as written in comments: D- followed by exactly three digits.
    private static readonly Regex DecisionToken =
        new(@"\bD-[0-9]{3}\b", RegexOptions.Compiled);

    /// <summary>The remaining backlog, measured 2026-08-06. Lower a number in the
    /// same changeset that sweeps its layer — never raise one. A layer that reaches
    /// zero should move to <see cref="SweptLayers"/> so it is pinned rather than
    /// merely bounded.</summary>
    public static TheoryData<string, int> RemainingLayers => new()
    {
        { "src/Backend/SIMF.Infrastructure", 1319 },
        { "src/Shared",                      1230 },
        { "src/ControlPanel",                 891 },
        { "src/Backend/SIMF.Api",             587 },
        { "src/Backend/SIMF.Application",     530 },
        { "src/Website",                       36 },
    };

    /// <summary>Layers that are swept and must stay at zero.</summary>
    public static TheoryData<string> SweptLayers => new() { "src/Backend/SIMF.Domain" };

    [Theory]
    [MemberData(nameof(SweptLayers))]
    public void A_swept_layer_carries_no_decision_provenance(string layer)
    {
        var offenders = SourceFiles(layer)
            .Select(file => (File: file, Tokens: DecisionToken.Matches(File.ReadAllText(file)).Count))
            .Where(hit => hit.Tokens > 0)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{layer} is swept and must stay free of decision ids in comments — "
            + "record the decision in docs/decisions/DECISIONS_LOG.md and write the "
            + "comment to explain the reasoning instead. Offending files: "
            + string.Join(", ", offenders.Select(o => $"{Relative(o.File)} ({o.Tokens})")));
    }

    [Theory]
    [MemberData(nameof(RemainingLayers))]
    public void An_unswept_layer_matches_its_recorded_backlog(string layer, int expected)
    {
        var actual = SourceFiles(layer)
            .Sum(file => DecisionToken.Matches(File.ReadAllText(file)).Count);

        // Exact, not "at most": a ceiling nobody lowers stops describing anything.
        Assert.True(
            actual == expected,
            $"{layer} carries {actual} decision id(s) in source, but this test records "
            + $"{expected}. If you swept the layer, lower the number here in the same "
            + "changeset (and move it to SweptLayers at zero). If the count went UP, a "
            + "new comment is carrying provenance that belongs in "
            + "docs/decisions/DECISIONS_LOG.md.");
    }

    private static IEnumerable<string> SourceFiles(string layer)
    {
        var root = Path.Combine(RepoRoot(), layer.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(root), $"Layer not found: {layer}");

        return Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(file => !IsBuildOutput(file));
    }

    // bin/ and obj/ hold generated copies of the same sources, which would count twice.
    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);

    private static string Relative(string file) =>
        Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');

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
}
