// An error code is written in ErrorCodes.cs and referenced by constant
// everywhere else. This test is the enforcement of that file's own rule.
//
// ErrorCodes.cs opens with: "Code strings are defined here once and never
// written as literals elsewhere." On 2026-08-21 six sites were doing exactly
// that - two in ArchiveEndpoints and four in GateOperatorService - and nothing
// noticed, because a duplicated literal compiles, ships, and behaves
// identically right up until someone edits one copy.
//
// Why that matters more here than the usual magic-string argument: several of
// these strings are a FROZEN WIRE CONTRACT. ErrorCodes.cs says so at the media
// and archive groups - "Promoted from the module-local consts; string values
// are the wire contract" - which is also why those five are lower_snake among
// 288 UPPER_SNAKE siblings and must STAY lower_snake. A literal copy is a
// second place the contract lives, and the copy is the one that drifts. The
// constant is the single point of truth; grep for the identifier finds every
// caller, grep for a string does not.
//
// Scope: the value of each ErrorCodes constant, searched across src/ (.cs and
// .razor), excluding build output and ErrorCodes.cs itself. Tests are excluded
// deliberately - a test asserting a literal wire value is pinning the contract
// on purpose, which is the opposite of the defect.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

public sealed class ErrorCodeLiteralTests
{
    private const string CatalogPath = "src/Shared/SIMF.Common/ErrorCodes.cs";

    [Fact]
    public void No_error_code_value_is_written_as_a_literal_outside_the_catalog()
    {
        var root = RepoRoot();
        var catalog = Path.Combine(root, CatalogPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(catalog), $"Expected the catalog at {catalog}");

        // Grouped, not ToDictionary. Two constants MAY share one wire value — an
        // alias is a normal thing to add and nothing in ErrorCodes.cs forbids it —
        // and ToDictionary would throw on the first duplicate before a single file
        // was scanned, turning a benign catalogue edit into an opaque
        // ArgumentException from a test that appears to be about something else.
        var byValue = Regex
            .Matches(File.ReadAllText(catalog), @"public const string (\w+)\s*=\s*""([^""]+)""")
            .GroupBy(m => m.Groups[2].Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => string.Join(" / ", group.Select(m => m.Groups[1].Value)),
                StringComparer.Ordinal);

        Assert.NotEmpty(byValue);

        var offenders = new List<string>();
        foreach (var file in SourceFiles(root))
        {
            if (string.Equals(file, catalog, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match literal in Regex.Matches(lines[i], @"""([^""]+)"""))
                {
                    if (byValue.TryGetValue(literal.Groups[1].Value, out var name))
                    {
                        offenders.Add(
                            $"  {Relative(root, file)}:{i + 1}"
                            + $" writes \"{literal.Groups[1].Value}\" — use ErrorCodes.{name}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "An error code is written as a raw string instead of its ErrorCodes constant:\n"
            + string.Join("\n", offenders.Distinct().OrderBy(o => o, StringComparer.Ordinal))
            + "\n\nErrorCodes.cs states the rule itself: code strings are defined there once "
            + "and never written as literals elsewhere. Several of these values are a frozen "
            + "wire contract, so a literal copy is a second home for the contract — and the "
            + "copy is the one that drifts when somebody edits the other. Reference the "
            + "constant; do NOT change the string value to make this pass.\n\n"
            + "If the flagged text is NOT a code being passed to anything — a value quoted "
            + "inside a comment, or an unrelated string that happens to read the same — then "
            + "this scan cannot tell the difference: it matches any double-quoted run on the "
            + "line. Reword the prose so it does not spell the value, or narrow the scan. "
            + "Do not silence it by editing the catalogue.");
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');

    private static IEnumerable<string> SourceFiles(string root)
    {
        var src = Path.Combine(root, "src");
        return Directory
            .EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }

    // Anchored on SIMF.slnx, matching the sibling ratchets. `.git` is a FILE in
    // a git worktree, so a walk-up testing for the directory lands on the wrong
    // tree and the guard passes vacuously - which has happened here before.
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
