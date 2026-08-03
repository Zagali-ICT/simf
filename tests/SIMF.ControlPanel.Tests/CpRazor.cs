namespace SIMF.ControlPanel.Tests;

/// <summary>
/// The Control Panel's Razor source, read once per test run, plus the one
/// component-tag scanner the markup ratchets share.
///
/// <para>Several ratchets in this project assert things about CP markup:
/// localized labels, destructive-action confirms, and the action-permission gates.
/// They all need the same three primitives — find the repo root, read the .razor
/// corpus, and pull one component's opening tags out of it with their attributes.
/// Those primitives were written twice with different rigour, and the weaker copy
/// ended up guarding the 145 permission attributes of D-830: it tracked only double
/// quotes, so a single-quoted attribute value containing '&gt;' would truncate the tag
/// and make every later attribute invisible to the guard. One implementation now,
/// and it is the stronger one.</para>
///
/// <para>The corpus is immutable for the lifetime of the run, so it is read once
/// rather than re-walked and re-decoded by every fact.</para>
/// </summary>
internal static class CpRazor
{
    /// <summary>One component usage: which file, which line, and the full opening
    /// tag text including every attribute.</summary>
    internal sealed record OpeningTag(string File, int Line, string Text);

    /// <summary>One .razor file: its repo-relative path (forward slashes, stable in
    /// assertion messages) and its contents.</summary>
    internal sealed record RazorFile(string Relative, string FullPath, string Text);

    /// <summary>The repository root, located by the solution file.
    ///
    /// <para>Every other root-finder in the test suite uses this same sentinel. One
    /// used "does a src/ControlPanel directory exist here" instead, which happens to
    /// agree while you only ever walk back down into that directory — and stops
    /// agreeing the moment you walk out to src/Shared, which the grid ratchet now
    /// does. A wrong root gives a silently empty scan, not an exception, so the
    /// guard would report clean.</para></summary>
    internal static string Root { get; } = FindRoot();

    internal static string ComponentsDir { get; } = Path.Combine(
        Root, "src", "ControlPanel", "SIMF.ControlPanel", "Components");

    internal static string PagesDir { get; } = Path.Combine(ComponentsDir, "Pages");

    internal static string DataGridPath { get; } = Path.Combine(
        Root, "src", "Shared", "SIMF.Components", "Forms", "SimfDataGrid.razor");

    /// <summary>Every .razor file under Components/ (pages, layouts and shared CP components).</summary>
    internal static IReadOnlyList<RazorFile> Components => ComponentCorpus.Value;

    /// <summary>Every .razor file under Components/Pages/ — the routable pages.</summary>
    internal static IReadOnlyList<RazorFile> Pages => PageCorpus.Value;

    private static readonly Lazy<IReadOnlyList<RazorFile>> ComponentCorpus = new(() => Read(ComponentsDir));
    private static readonly Lazy<IReadOnlyList<RazorFile>> PageCorpus = new(() => Read(PagesDir));

    /// <summary>
    /// Every <c>&lt;ComponentName ...&gt;</c> opening tag in the given files, with its
    /// full attribute list.
    ///
    /// <para>A tag spans several lines, so a line-by-line grep misses attributes; and
    /// a <c>[^&gt;]*</c> regex is WRONG here, because Razor attribute values routinely
    /// contain '&gt;' inside a lambda (<c>RowKey="@(row =&gt; row.Id.ToString())"</c>),
    /// which truncates the match at the arrow. So scan forward tracking quote state,
    /// single AND double, and stop at the first '&gt;' outside a quoted value.</para>
    /// </summary>
    internal static IEnumerable<OpeningTag> OpeningTags(
        IEnumerable<RazorFile> files, string componentName)
    {
        var open = "<" + componentName;

        foreach (var file in files)
        {
            var source = file.Text;
            for (var start = source.IndexOf(open, StringComparison.Ordinal);
                 start >= 0;
                 start = source.IndexOf(open, start + 1, StringComparison.Ordinal))
            {
                // "<SimfDataGridColumn" must not match "<SimfDataGrid".
                var after = start + open.Length;
                if (after < source.Length
                    && (char.IsLetterOrDigit(source[after]) || source[after] == '_'))
                {
                    continue;
                }

                var quote = '\0';
                var end = -1;
                for (var i = after; i < source.Length; i++)
                {
                    var c = source[i];
                    if (quote != '\0') { if (c == quote) { quote = '\0'; } continue; }
                    if (c is '"' or '\'') { quote = c; continue; }
                    if (c == '>') { end = i; break; }
                }
                if (end < 0) { continue; }

                yield return new OpeningTag(
                    file.Relative,
                    source.Take(start).Count(c => c == '\n') + 1,
                    source[start..(end + 1)]);
            }
        }
    }

    /// <summary>True when the tag sets the named attribute at all.</summary>
    internal static bool HasAttribute(string tag, string name) =>
        AttributeValue(tag, name) is not null;

    /// <summary>The value the tag assigns to the named attribute, with any leading
    /// Razor '@' stripped, or null when the attribute is absent. Reading it OFF THE
    /// TAG rather than off the whole file matters: three CP pages host several grids,
    /// and a whole-file scan attributes one grid's value to another.</summary>
    internal static string? AttributeValue(string tag, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            tag,
            @"\b" + System.Text.RegularExpressions.Regex.Escape(name) + @"\s*=\s*""@?(?<value>[^""]*)""");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static IReadOnlyList<RazorFile> Read(string directory) =>
        Directory.EnumerateFiles(directory, "*.razor", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new RazorFile(
                Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/'),
                path,
                File.ReadAllText(path)))
            .ToList();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
    }
}
