using Xunit;

namespace SIMF.Web.Tests;

/// <summary>
/// D-830 — the public Website may not use the shared components' permission gate.
///
/// <para><c>SimfActionGate</c> and the <c>SimfDataGrid</c> <c>*Permission</c> parameters
/// live in <c>SIMF.Components</c>, which the Website links. They render an
/// <c>AuthorizeView</c>, which needs a cascading <c>Task&lt;AuthenticationState&gt;</c> —
/// and the Website registers no authorization services and cascades no authentication
/// state at all. Today nothing reaches that branch, because the gate is transparent
/// while every permission is null and no Website page renders a grid.</para>
///
/// <para>That is a property, not a guarantee, and the failure mode is bad: a
/// <c>InvalidOperationException</c> at render time on a public page, in the one
/// application with no admin watching it. This fails the build instead.</para>
/// </summary>
public sealed class SharedComponentAuthorizationTests
{
    [Fact]
    public void No_website_markup_uses_the_shared_permission_gate()
    {
        var website = Path.Combine(FindRepoRoot(), "src", "Website", "SIMF.Web");

        var offenders = Directory
            .EnumerateFiles(website, "*.razor", SearchOption.AllDirectories)
            .Select(path => (Path: Path.GetRelativePath(website, path), Text: File.ReadAllText(path)))
            .Where(file => file.Text.Contains("SimfActionGate", StringComparison.Ordinal)
                || System.Text.RegularExpressions.Regex.IsMatch(
                    file.Text,
                    @"\b(Add|Edit|Delete|Import|Export|Approve|Reject)Permission\s*=\s*"""))
            .Select(file => file.Path.Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "The Website has no authentication cascade, so a non-null permission on a "
            + "shared component throws at render time on a public page. Either the page "
            + "does not need the gate, or the Website needs CascadingAuthenticationState "
            + "and the authorization services first:\n  " + string.Join("\n  ", offenders));
    }

    private static string FindRepoRoot()
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
