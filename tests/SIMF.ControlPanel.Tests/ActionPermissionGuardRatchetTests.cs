using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// The project's own hard rule: "Wrap action buttons in
/// <c>&lt;AuthorizedAction Permission="PermissionCatalog.X.Y"&gt;</c>".
///
/// <para><b>What this catches.</b> A page gated on <c>X.View</c> whose Save /
/// Approve / Notify button needs <c>X.Edit</c> (or <c>Create</c> / <c>Delete</c> /
/// <c>Notify</c>). A View-only admin reaches the page, fills the form, presses the
/// button, and learns only from the API's 403 that they were never allowed to. The
/// API gates correctly in every case here — so this is a usability and consistency
/// defect, not a hole — but it is the difference between a system that tells you
/// what you may do and one that lets you find out by failing.</para>
///
/// <para>Found on <c>/admin/operations</c> during a live pass, then measured across
/// the whole Control Panel: of 96 permission-gated pages, 30 already gated their
/// actions and 36 are read-only with nothing to gate. Four had the gap -
/// <c>OperationsToggles</c>, <c>FaqManager</c>, <c>RatingConfig</c> and
/// <c>VipsList</c> - and all four are fixed, which is why the reviewed-exception
/// list below is EMPTY.</para>
///
/// <para>The staleness test is what closed them out: it named all four the moment
/// their buttons were wrapped and refused to pass until the entries were deleted.
/// An exception list that outlives the defect it records is worse than no list,
/// because the next reader trusts it.</para>
/// </summary>
public sealed class ActionPermissionGuardRatchetTests
{
    /// <summary>The page's own gate, e.g. <c>RequirePermission(PermissionCatalog.Faq.View)</c>.</summary>
    private static readonly Regex PagePermission = new(
        @"RequirePermission\(PermissionCatalog\.(?<group>[A-Za-z0-9_]+)\.(?<action>[A-Za-z0-9_]+)\)",
        RegexOptions.Compiled);

    /// <summary>A button that commits something rather than navigating or closing.</summary>
    private static readonly Regex MutatingButton = new(
        @"OnClick=""@?(?<handler>Save|Delete|Approve|Reject|Submit|Remove|Toggle|OnNotify)[A-Za-z]*""",
        RegexOptions.Compiled);

    /// <summary>
    /// Reviewed exceptions: a View-gated page holding a mutating button that is not
    /// wrapped. Keep the justification with the entry, and do not add one without
    /// saying what a View-only holder currently experiences.
    /// </summary>
    private static readonly Dictionary<string, string> ReviewedExceptions = new()
    {
        // Empty, and that is the point. An entry here is a page shipping a button
        // its viewer cannot use; each must carry a justification saying what a
        // View-only holder sees. The four original entries were deleted when the
        // pages were fixed - see D-828.
    };

    [Fact]
    public void No_new_page_ships_a_mutating_button_without_an_action_gate()
    {
        var offenders = new List<string>();

        foreach (var razorPath in EnumeratePages())
        {
            var razor = File.ReadAllText(razorPath);

            var permission = PagePermission.Match(razor);
            if (!permission.Success) { continue; }

            // Only a View gate leaves room for the gap. A page gated on X.Edit
            // already excludes everyone who cannot use its buttons.
            if (permission.Groups["action"].Value != "View") { continue; }

            if (razor.Contains("AuthorizedAction", StringComparison.Ordinal)) { continue; }
            if (!MutatingButton.IsMatch(razor)) { continue; }

            var key = Relative(razorPath);
            if (ReviewedExceptions.ContainsKey(key)) { continue; }

            offenders.Add(
                $"{key} — gated on {permission.Groups["group"].Value}.View, but "
                + $"'{MutatingButton.Match(razor).Groups["handler"].Value}...' is not "
                + "wrapped in <AuthorizedAction>");
        }

        Assert.True(
            offenders.Count == 0,
            "Page(s) gate the PAGE on X.View but leave a mutating button ungated, so a "
            + "View-only admin can drive an action the API will refuse. Wrap it in "
            + "<AuthorizedAction Permission=\"PermissionCatalog.X.Edit\">, or add a "
            + "reviewed exception saying what the holder sees:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_reviewed_exception_list_has_no_stale_entries()
    {
        // A fixed page that stays on the list is a false record of a defect, and the
        // next reader trusts the list. Deleting the entry is part of the fix.
        var stale = ReviewedExceptions.Keys
            .Where(key =>
            {
                var path = Path.Combine(PagesRoot(), key["Pages/".Length..]);
                if (!File.Exists(path))
                {
                    return true;   // page renamed or deleted
                }
                var razor = File.ReadAllText(path);
                return razor.Contains("AuthorizedAction", StringComparison.Ordinal)
                    || !MutatingButton.IsMatch(razor);
            })
            .ToList();

        Assert.True(
            stale.Count == 0,
            "Reviewed exception(s) no longer apply — the page now gates its actions, "
            + "or no longer exists. Delete the entry:\n  " + string.Join("\n  ", stale));
    }

    private static string PagesRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(
            Path.Combine(directory.FullName, "src", "ControlPanel")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                "Could not locate the repository root from " + AppContext.BaseDirectory)
            : Path.Combine(directory.FullName, "src", "ControlPanel",
                "SIMF.ControlPanel", "Components", "Pages");
    }

    private static IEnumerable<string> EnumeratePages() =>
        Directory.EnumerateFiles(PagesRoot(), "*.razor", SearchOption.AllDirectories);

    private static string Relative(string path) =>
        "Pages/" + Path.GetRelativePath(PagesRoot(), path).Replace('\\', '/');
}
