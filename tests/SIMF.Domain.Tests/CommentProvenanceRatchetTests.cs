using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// Guards the comment standard: source comments explain why the code is shaped as
/// it is, and decision provenance lives in <c>docs/decisions/DECISIONS_LOG.md</c>
/// rather than being copied into the code, where it rots because the log is
/// maintained and the comment is not.
///
/// <para>Every layer is swept and pinned at zero. While the sweep was in progress
/// the unswept layers carried an exact recorded count that could only go down, so
/// "we will get to it" was a number rather than an intention; that scaffolding is
/// gone now the count is zero everywhere. A prose rule cannot detect the next
/// token added; this can.</para>
/// </summary>
public sealed class CommentProvenanceRatchetTests
{
    // A decision id as written in comments: D- followed by exactly three digits.
    private static readonly Regex DecisionToken =
        new(@"\bD-[0-9]{3}\b", RegexOptions.Compiled);

    /// <summary>Every layer, swept and pinned at zero.</summary>
    private static readonly string[] Layers =
    [
        "src/Backend/SIMF.Domain",
        "src/Backend/SIMF.Application",
        "src/Backend/SIMF.Api",
        "src/Backend/SIMF.Infrastructure",
        "src/Shared",
        "src/ControlPanel",
        "src/Website",
        "src/Tools",
    ];

    public static TheoryData<string> SweptLayers => new(Layers);

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

    [Fact]
    public void The_swept_set_covers_every_source_layer()
    {
        // A layer missing from SweptLayers is not guarded at all, and the gap would
        // be invisible: the other test would still pass on the layers it does list.
        var guarded = Layers.ToHashSet(StringComparer.Ordinal);

        var actual = Directory
            .EnumerateDirectories(Path.Combine(RepoRoot(), "src"), "*", SearchOption.AllDirectories)
            .Where(directory => Directory.EnumerateFiles(directory, "*.csproj").Any())
            .Select(directory => Path.GetRelativePath(RepoRoot(), directory).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/") && !path.Contains("/obj/"))
            .ToList();

        var unguarded = actual
            .Where(project => !guarded.Any(layer =>
                project.Equals(layer, StringComparison.Ordinal)
                || project.StartsWith(layer + "/", StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            unguarded.Count == 0,
            "These source projects are not covered by any entry in SweptLayers, so "
            + "decision ids could accumulate in them unnoticed: "
            + string.Join(", ", unguarded));
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
