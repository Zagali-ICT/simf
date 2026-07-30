// Tests: D-799 — a source-scan ratchet over the CP .razor sources, in the same
// shape as CpMarkupHygieneTests / AccessibilityMarkupTests.
//
// WHY THIS EXISTS. SimfDataGrid deliberately has no built-in confirmation: its
// per-row trash button, its bulk-delete toolbar button and its context-menu Delete
// all invoke their callback directly. That is the right design — 25 of the 38
// OnDeleteOne sites route to a D-353 ViewDelete page which confirms there, and a
// grid-level dialog would double-confirm every one of them. The cost of that design
// is that EVERY page owns its own guard, and a page that forgets one ships a silent
// one-click delete. Seven pages had forgotten (D-799).
//
// Fixing those seven does not stop the eighth. This does. The rule it enforces:
//
//     A method bound to OnDeleteOne / OnDeleteSelected / OnApproveSelected must NOT
//     itself perform the mutation. It must STAGE the action — set a target, raise a
//     flag, or route to a ViewDelete form — and let a confirmation surface commit it.
//
// A new destructive control wired straight to a deleteJson/postJson call fails the
// build and has to be argued for here, with a justification, exactly as CLAUDE.md §4
// prescribes for the anonymous-endpoint surface ("a prose rule cannot detect the
// 18th; a test can").
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class DestructiveActionGuardRatchetTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string CpComponentsDir = "src/ControlPanel/SIMF.ControlPanel/Components";

    /// <summary>The grid callbacks that commit something irreversible.</summary>
    private static readonly Regex DestructiveBinding = new(
        @"On(?<callback>DeleteOne|DeleteSelected|ApproveSelected)\s*=\s*""(?<handler>[^""]+)""",
        RegexOptions.Compiled);

    /// <summary>A handler that reaches any of these is committing, not staging.</summary>
    private static readonly string[] MutatingCalls =
        ["deleteJson", "postJson", "putJson", "patchJson"];

    /// <summary>Reviewed exceptions. Each entry is a handler that DOES call the API
    /// directly but guards it first by its own means — keep the justification with
    /// the entry, and do not add to this list without one.</summary>
    private static readonly Dictionary<string, string> ReviewedExceptions = new()
    {
        ["Pages/Admin/MeetingTablesList.razor:OnDeleteTableAsync"] =
            "Guards with a native window.confirm before the DELETE "
            + "(MeetingTablesList.razor.cs:238). A fourth guard mechanism in the CP — "
            + "un-themed and without RTL treatment — so it is accepted, not endorsed; "
            + "folding it into SimfConfirm is a tracked follow-up.",
        ["Pages/Admin/MeetingTablesList.razor:OnReleaseAllocationAsync"] =
            "Same native window.confirm guard (MeetingTablesList.razor.cs:373).",
    };

    [Fact]
    public void No_destructive_grid_callback_commits_without_a_confirmation()
    {
        var offenders = new List<string>();

        foreach (var razorPath in EnumerateRazorSources())
        {
            var razor = File.ReadAllText(razorPath);
            // The handler may live in the page's own code-behind OR in the base it
            // declares with @inherits — the three pending queues share their bulk
            // approve/reject through PendingApprovalPageBase.
            var codeBehind = ReadIfExists(razorPath + ".cs") + BaseClassSource(razorPath, razor);

            foreach (Match binding in DestructiveBinding.Matches(razor))
            {
                var handler = binding.Groups["handler"].Value.Trim();

                // An inline lambda is only safe if it stages — i.e. it does not
                // reach a mutating call itself. Lambdas that call Ask*/Open* are the
                // established staging shape (FaqManager, RatingConfig, Notifications).
                if (handler.StartsWith('@'))
                {
                    if (MutatingCalls.Any(c => handler.Contains(c, StringComparison.Ordinal)))
                    {
                        offenders.Add($"{Relative(razorPath)}: inline {binding.Groups["callback"].Value} "
                            + "lambda calls the API directly");
                    }
                    continue;
                }

                var key = $"{Relative(razorPath)}:{handler}";
                if (ReviewedExceptions.ContainsKey(key)) continue;

                var body = MethodBody(codeBehind, handler);
                if (body is null)
                {
                    // Bound to a member this scan cannot see (a base class, or a
                    // component parameter). Not provably safe — say so rather than
                    // passing silently.
                    offenders.Add($"{Relative(razorPath)}: handler '{handler}' not found in the "
                        + "code-behind; cannot verify it stages rather than commits");
                    continue;
                }

                var committed = MutatingCalls
                    .Where(c => body.Contains(c, StringComparison.Ordinal))
                    .ToList();
                if (committed.Count > 0)
                {
                    offenders.Add($"{Relative(razorPath)}: '{handler}' is wired straight to "
                        + $"{binding.Groups["callback"].Value} and calls {string.Join("/", committed)} "
                        + "itself — the operator gets no confirmation");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "D-799: a destructive grid action must stage a confirmation, never commit on the "
            + "first click. SimfDataGrid has no built-in confirm, so the page owns the guard. "
            + "Offenders:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_reviewed_exception_still_exists()
    {
        var stale = ReviewedExceptions.Keys
            .Where(key =>
            {
                var (file, handler) = (key[..key.IndexOf(':')], key[(key.IndexOf(':') + 1)..]);
                var path = Path.Combine(RepoRoot, CpComponentsDir, file.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(path)
                    || !DestructiveBinding.Matches(File.ReadAllText(path))
                        .Any(m => m.Groups["handler"].Value.Trim() == handler);
            })
            .ToList();

        Assert.True(stale.Count == 0,
            "These reviewed exceptions no longer match a real destructive binding — the page was "
            + "fixed or removed, so drop the entry: " + string.Join(", ", stale));
    }

    private static string ReadIfExists(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    /// <summary>The source of the class a page declares with <c>@inherits</c>, so a
    /// handler inherited from a shared page base is still verifiable.</summary>
    private static string BaseClassSource(string razorPath, string razor)
    {
        var inherits = Regex.Match(razor, @"^@inherits\s+([\w.]+)\s*$", RegexOptions.Multiline);
        if (!inherits.Success) return string.Empty;

        var typeName = inherits.Groups[1].Value.Split('.')[^1];
        var componentsRoot = Path.Combine(RepoRoot, CpComponentsDir);
        return string.Concat(
            Directory.EnumerateFiles(componentsRoot, typeName + ".cs", SearchOption.AllDirectories)
                     .Select(File.ReadAllText));
    }

    /// <summary>Extracts a method body by brace-matching from its signature.</summary>
    private static string? MethodBody(string source, string methodName)
    {
        var signature = new Regex(
            @"\b(?:private|protected|internal|public)[^\r\n(]*\b"
            + Regex.Escape(methodName) + @"\s*\(");
        var match = signature.Match(source);
        if (!match.Success) return null;

        var open = source.IndexOf('{', match.Index + match.Length);
        if (open < 0) return null;

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
        }
        return null;
    }

    private static IEnumerable<string> EnumerateRazorSources() =>
        Directory.EnumerateFiles(
            Path.Combine(RepoRoot, CpComponentsDir), "*.razor", SearchOption.AllDirectories);

    private static string Relative(string fullPath) =>
        Path.GetRelativePath(Path.Combine(RepoRoot, CpComponentsDir), fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory)
            : dir.FullName;
    }
}
