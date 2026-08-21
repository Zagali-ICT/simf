// Enforces ErrorCodes.cs's own rule: code strings are defined there once and
// never written as literals elsewhere. Several values are a frozen wire
// contract, so a literal copy is a second home for the contract.
//
// Scans src/ (.cs, .razor) excluding build output and the catalogue. Tests are
// excluded: one asserting a literal wire value is pinning it on purpose.
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

        // Grouped, not ToDictionary: two constants may legitimately alias one
        // value, and ToDictionary would throw before scanning anything.
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
            + "\n\nReference the constant. Do NOT change the string value to make this "
            + "pass — several are a frozen wire contract, which is why five are lower_snake "
            + "among 288 UPPER_SNAKE siblings.\n"
            + "If the match is a value quoted in prose rather than a code being passed, "
            + "reword it: this scan matches any quoted run on the line.");
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

    // Anchored on SIMF.slnx, not `.git`: that is a FILE in a worktree, so a
    // walk-up testing for the directory audits the wrong tree and passes.
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
