using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// The project's own hard rule: every action control is gated on the permission that
/// gates the endpoint it calls.
///
/// <para><b>What this catches.</b> A page gated on <c>X.View</c> whose Save / Approve /
/// Notify button needs <c>X.Edit</c>. A View-only admin reaches the page, fills the
/// form, presses the button, and learns only from the API's 403 that they were never
/// allowed to. The API gates correctly in every case here — so this is a usability and
/// consistency defect, not a hole — but it is the difference between a system that
/// tells you what you may do and one that lets you find out by failing.</para>
///
/// <para><b>Two kinds of control, two rules.</b> A page writes some of its buttons, and
/// <see cref="No_new_page_ships_a_mutating_button_without_an_action_gate"/> checks those
/// are wrapped in a gate component. <c>SimfDataGrid</c> writes the rest — Add, Edit,
/// Delete, Duplicate, Paste, Import, Export, Approve, Reject, across a toolbar, a
/// row-end cell and a right-click menu — from the callbacks the page wires, and no
/// wrapper can reach them; those are checked by
/// <see cref="No_page_wires_a_grid_callback_without_its_permission"/>.</para>
///
/// <para><b>Census.</b> The page-written gap was found on <c>/admin/operations</c> during
/// a live pass and measured across the Control Panel: 4 pages had it (D-828), then 8 more
/// once the check was made per control rather than per page (D-829). The grid-written gap
/// was 45 pages and 179 permission attributes, and was invisible to both earlier passes because the
/// markup lives in the shared component, not the page (D-830). Both reviewed-exception
/// lists are near-empty as a result; an entry is a page shipping a control its holder
/// cannot use, and must carry a justification.</para>
/// </summary>
public sealed class ActionPermissionGuardRatchetTests
{
    /// <summary>The page's own gate, e.g. <c>RequirePermission(PermissionCatalog.Faq.View)</c>.</summary>
    private static readonly Regex PagePermission = new(
        @"RequirePermission\(PermissionCatalog\.(?<group>[A-Za-z0-9_]+)\.(?<action>[A-Za-z0-9_]+)\)",
        RegexOptions.Compiled);

    /// <summary>A button that commits something rather than navigating or closing.
    ///
    /// <para>D-831 added <c>Confirm</c>. Its absence was a real hole rather than an
    /// oversight in taste: the commit button of a confirm modal is named
    /// <c>ConfirmXxxAsync</c> by convention here, so the one control that actually
    /// posts was the one verb the guard could not see, on 11 pages.</para></summary>
    private static readonly Regex MutatingButton = new(
        @"OnClick=""@?(?<handler>Save|Delete|Approve|Reject|Submit|Remove|Toggle|OnNotify|Confirm)[A-Za-z]*""",
        RegexOptions.Compiled);

    /// <summary>
    /// Reviewed exceptions to the page-written rule: a View-gated page holding a
    /// mutating button that is not wrapped. Keep the justification with the entry, and
    /// do not add one without saying what a View-only holder currently experiences.
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

        foreach (var (file, group, action) in GatedPages())
        {
            // Only a View gate leaves room for the gap. A page gated on X.Edit
            // already excludes everyone who cannot use its buttons.
            if (action != "View") { continue; }
            if (ReviewedExceptions.ContainsKey(file.Relative)) { continue; }

            // PER BUTTON, not per page. The first cut of this test skipped any
            // page containing the string "AuthorizedAction" anywhere, which meant
            // one gated button hid every ungated one beside it — and it did:
            // four more pages (BadgeRequests, SessionModerators, MeetingTables,
            // DocumentRequests) were waved through on their FIRST run because
            // they gated something else. A guard that reports clean while the
            // defect is present is worse than no guard, so containment is
            // checked for each control individually.
            foreach (Match button in MutatingButton.Matches(file.Text))
            {
                if (IsInsideActionGate(file.Text, button.Index)) { continue; }

                offenders.Add(
                    $"{file.Relative} — gated on {group}.View, but "
                    + $"'{button.Groups["handler"].Value}...' is not wrapped in an action gate");
            }
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
        var pages = CpRazor.Pages.ToDictionary(page => page.Relative, page => page.Text);

        var stale = ReviewedExceptions.Keys
            .Where(key => !pages.TryGetValue(key, out var razor)
                || MutatingButton.Matches(razor)
                    .All(button => IsInsideActionGate(razor, button.Index)))
            .Concat(ReviewedGridExceptions.Keys.Where(key => !pages.ContainsKey(key)))
            .ToList();

        Assert.True(
            stale.Count == 0,
            "Reviewed exception(s) no longer apply — the page now gates its actions, "
            + "or no longer exists. Delete the entry:\n  " + string.Join("\n  ", stale));
    }

    // ---------------------------------------------------------------------
    // D-830 — the same rule, for the buttons the page does not write.
    //
    // SimfDataGrid renders its own Add / Edit / Delete / Duplicate / Paste /
    // Import / Export / Approve / Reject controls from the callbacks a page
    // wires, so no wrapper can reach them from the page and the test above
    // cannot see them. That is why it reported clean while 45 pages offered
    // those controls to admins the API would refuse. The grid now takes a
    // permission code per callback; this pins the pairing.
    // ---------------------------------------------------------------------

    /// <summary>Each gated grid callback and the parameter that must carry its code.
    ///
    /// <para>Delete is one code for two callbacks: deleting one row and deleting the
    /// selection is the same verb on the same resource. Add is one code for three:
    /// Add, Duplicate and Paste all create, the catalogue has no Duplicate or Paste
    /// code, and the API gates <c>POST /admin/admins/duplicate</c> on the same
    /// <c>Admins.Create</c> as plain create.</para></summary>
    private static readonly (string Parameter, string[] Callbacks)[] GridActionGates =
    [
        ("AddPermission",     ["OnAdd", "OnDuplicateOne", "OnPaste"]),
        ("EditPermission",    ["OnEditOne"]),
        ("DeletePermission",  ["OnDeleteOne", "OnDeleteSelected"]),
        ("ImportPermission",  ["OnImport"]),
        ("ExportPermission",  ["OnExport"]),
        ("ApprovePermission", ["OnApproveSelected"]),
        ("RejectPermission",  ["OnRejectSelected"]),
    ];

    /// <summary>Grid callbacks that need no gate, each for a stated reason. Anything
    /// not listed here and not in <see cref="GridActionGates"/> fails the
    /// classification test below.</summary>
    private static readonly Dictionary<string, string> UngatedGridCallbacks = new()
    {
        ["OnQueryChanged"] = "sort / filter / page — reading, which the page's own gate bought",
        ["OnSelectionChanged"] = "ticking a checkbox commits nothing",
        ["OnDetailsOne"] = "opens the same record read-only",
        ["OnCopySelected"] = "copies rows already on screen to the clipboard",
        ["OnCopyOne"] = "copies one row already on screen to the clipboard",
    };

    /// <summary>Reviewed exceptions to the grid rule. An entry means the page gates the
    /// control some other way, and must say how.</summary>
    private static readonly Dictionary<string, string> ReviewedGridExceptions = new()
    {
        ["src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipRegistration.razor"] =
            "Gates the CALLBACK rather than the button: OnInitializedAsync resolves "
            + "Visitors.RegisterOnsite / Visitors.Edit through IAuthorizationService into "
            + "_canRegister / _canEdit, and passes `default` instead of a handler when "
            + "either is false. The grid then renders no button at all, which is strictly "
            + "stronger than hiding one. Do not 'fix' this by adding parameters as well.",
    };

    [Fact]
    public void No_page_wires_a_grid_callback_without_its_permission()
    {
        var offenders = new List<string>();

        // NOT restricted to View-gated pages. That filter is right for the
        // page-written rule above — a page gated on X.Edit already excludes anyone
        // who cannot edit — but it is wrong here, and provably so: GatesList is
        // gated on Gates.Manage and wires OnImport, which the API gates on the
        // SEPARATE code Gates.Import. A Manage holder without Import got the button
        // and a 403, and a View-only filter could never have seen it.
        foreach (var (file, group, action) in GatedPages())
        {
            if (ReviewedGridExceptions.ContainsKey(file.Relative)) { continue; }

            foreach (var tag in CpRazor.OpeningTags([file], "SimfDataGrid"))
            {
                foreach (var (parameter, callbacks) in GridActionGates)
                {
                    var wired = callbacks.FirstOrDefault(
                        callback => CpRazor.HasAttribute(tag.Text, callback));
                    if (wired is null) { continue; }
                    if (CpRazor.HasAttribute(tag.Text, parameter)) { continue; }

                    offenders.Add(
                        $"{file.Relative}:{tag.Line} — page gated on {group}.{action}, "
                        + $"grid wires {wired}, but has no {parameter}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "SimfDataGrid renders those buttons itself, so the holder can press them and "
            + "only the API's 403 tells them they could not. Set the parameter to the "
            + "permission that gates the ENDPOINT the button calls (not the page's own "
            + "gate, which everyone reaching the page already holds):\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_page_opens_a_confirm_dialog_without_its_permission()
    {
        // D-831 — the third shared component that renders a committing button the page
        // cannot wrap. SimfConfirm draws its own Confirm from OnConfirm, exactly as the
        // grid draws its Add from OnAdd, so it takes a Permission for the same reason.
        // Cancel is never gated: a holder who may not commit must still be able to
        // close the dialog.
        var offenders = new List<string>();

        foreach (var (file, group, action) in GatedPages())
        {
            foreach (var tag in CpRazor.OpeningTags([file], "SimfConfirm"))
            {
                if (!CpRazor.HasAttribute(tag.Text, "OnConfirm")) { continue; }
                if (CpRazor.HasAttribute(tag.Text, "Permission")) { continue; }

                offenders.Add(
                    $"{file.Relative}:{tag.Line} — page gated on {group}.{action}, "
                    + "confirm dialog has no Permission");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "SimfConfirm renders its Confirm button itself, so the page cannot wrap it. "
            + "Set Permission to the code that gates the endpoint the confirmed action "
            + "calls:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_component_tag_hides_a_razor_comment_among_its_attributes()
    {
        // Not a permission rule - a rule about how these permission comments get
        // written, added the moment it bit. A @* ... *@ inside a component tag's
        // ATTRIBUTE LIST compiles cleanly and then throws at render:
        //
        //   InvalidOperationException: Object of type 'SimfConfirm' does not have a
        //   property matching the name '@* ... *@'
        //
        // Razor treats it as an attribute name, not a comment. D-831 shipped one into
        // MeetingTablesList by explaining a permission choice in the tidiest-looking
        // place, and no bUnit test renders that page, so the whole suite stayed green
        // while /admin/meeting-tables threw on load. Put the comment ABOVE the tag.
        var offenders = new List<string>();

        foreach (var file in CpRazor.Components)
        {
            foreach (Match open in Regex.Matches(file.Text, @"<[A-Z][A-Za-z0-9_]*\b"))
            {
                var inQuote = false;
                for (var i = open.Index; i < file.Text.Length; i++)
                {
                    if (file.Text[i] == '"') { inQuote = !inQuote; }
                    else if (file.Text[i] == '>' && !inQuote)
                    {
                        if (file.Text[open.Index..i].Contains("@*", StringComparison.Ordinal))
                        {
                            offenders.Add(
                                $"{file.Relative}:{file.Text.Take(open.Index).Count(c => c == '\n') + 1}"
                                + $" — {open.Value} carries a Razor comment among its attributes");
                        }
                        break;
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A Razor comment inside a component tag is read as an attribute NAME and "
            + "throws at render time, which no compile and no untested page will catch. "
            + "Move it above the tag:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_shared_components_that_render_committing_buttons_still_take_a_permission()
    {
        // Both gates are one nullable parameter each, and either could be deleted by a
        // well-meaning cleanup without breaking a compile: every call site would simply
        // stop gating. Pin their existence.
        var grid = File.ReadAllText(CpRazor.DataGridPath);
        var confirm = File.ReadAllText(Path.Combine(
            CpRazor.Root, "src", "Shared", "SIMF.Components", "Forms", "SimfConfirm.razor"));

        Assert.Contains("[Parameter] public string? Permission { get; set; }", confirm);
        Assert.Contains("<SimfActionGate Permission=\"@Permission\">", confirm);
        Assert.Contains("<SimfActionGate Permission=\"@AddPermission\">", grid);
    }

    [Fact]
    public void A_grid_action_permission_is_never_the_pages_own_View_code()
    {
        // The copy-paste failure this rule invites: pasting the page's own gate into
        // the parameter. It compiles, it reads as gated, and it gates nothing, because
        // everyone who can open the page already holds View.
        //
        // Scoped to View-gated pages deliberately. On a page gated on a mutating code
        // the two being equal is often CORRECT — GatesList is gated on Gates.Manage and
        // its Add really does call an endpoint gated on Gates.Manage.
        var offenders = new List<string>();

        foreach (var (file, group, action) in GatedPages())
        {
            if (action != "View") { continue; }
            var pageCode = $"PermissionCatalog.{group}.{action}";

            foreach (var tag in CpRazor.OpeningTags([file], "SimfDataGrid"))
            {
                foreach (var (parameter, _) in GridActionGates)
                {
                    if (CpRazor.AttributeValue(tag.Text, parameter) == pageCode)
                    {
                        offenders.Add($"{file.Relative}:{tag.Line} — {parameter} is {pageCode}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A grid action is gated on the same code that gates the page, so it gates "
            + "nothing:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_grid_callback_is_classified_as_gated_or_ungated()
    {
        // The one-level-up version of the same problem: a NEW callback added to
        // SimfDataGrid would render a new button that no rule covers, and the test
        // above would keep reporting clean. Classify it here, and either give it a
        // permission parameter or say in writing why it needs none.
        var grid = File.ReadAllText(CpRazor.DataGridPath);

        var declared = Regex.Matches(
                grid, @"\[Parameter\]\s+public\s+EventCallback[^\s]*\s+(?<name>On[A-Za-z0-9_]+)")
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var known = GridActionGates.SelectMany(gate => gate.Callbacks)
            .Concat(UngatedGridCallbacks.Keys)
            .ToHashSet(StringComparer.Ordinal);

        var unclassified = declared.Except(known).OrderBy(name => name).ToList();
        var vanished = known.Except(declared).OrderBy(name => name).ToList();

        Assert.True(
            unclassified.Count == 0 && vanished.Count == 0,
            "SimfDataGrid's callback set no longer matches this test's classification. "
            + "Add each new callback to GridActionGates (with a permission parameter on "
            + "the grid) or to UngatedGridCallbacks (with the reason it needs none); drop "
            + "entries for callbacks that no longer exist.\n  unclassified: "
            + string.Join(", ", unclassified)
            + "\n  listed but gone: " + string.Join(", ", vanished));
    }

    [Fact]
    public void Every_grid_permission_parameter_is_covered_by_the_gate_table()
    {
        // The mirror of the test above, from the parameter side: a parameter added to
        // the grid that no rule requires would be settable, forgettable, and unchecked.
        var grid = File.ReadAllText(CpRazor.DataGridPath);

        var declared = Regex.Matches(
                grid, @"\[Parameter\]\s+public\s+string\?\s+(?<name>[A-Za-z0-9_]*Permission)\b")
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var listed = GridActionGates.Select(gate => gate.Parameter).ToHashSet(StringComparer.Ordinal);

        Assert.True(
            declared.SetEquals(listed),
            "SimfDataGrid's permission parameters and this test's gate table disagree.\n"
            + "  on the grid, not in the table: " + string.Join(", ", declared.Except(listed))
            + "\n  in the table, not on the grid: " + string.Join(", ", listed.Except(declared)));
    }

    /// <summary>
    /// Every CP page that declares a page-level permission, with the group and action
    /// it declares.
    ///
    /// <para>A page with NO <c>RequirePermission</c> is skipped, and today that is one
    /// file: <c>Account/Notifications.razor</c>, which is self-service — the grid deletes
    /// the signed-in user's OWN notifications, so there is no permission split to get
    /// wrong. A new unauthenticated page with a mutating grid would also be skipped, and
    /// would be caught instead by <c>CpNavigationPermissionTests</c>, which fails the
    /// build on an ungated admin page.</para>
    /// </summary>
    private static IEnumerable<(CpRazor.RazorFile File, string Group, string Action)> GatedPages()
    {
        foreach (var file in CpRazor.Pages)
        {
            var permission = PagePermission.Match(file.Text);
            if (!permission.Success) { continue; }
            yield return (file,
                permission.Groups["group"].Value,
                permission.Groups["action"].Value);
        }
    }

    /// <summary>
    /// True when the control at <paramref name="index"/> sits inside a gate block.
    ///
    /// <para>Both names count. <c>AuthorizedAction</c> is the Control Panel alias and
    /// <c>SimfActionGate</c> is the shared component it forwards to (D-830); the shared
    /// one is in scope in every CP page through <c>_Imports.razor</c>, so a page author
    /// who reaches for it is correctly gated and must not be reported as an offender.</para>
    ///
    /// <para>Nearest-tag-wins rather than a parse: if the closest opening tag before the
    /// control comes after the closest closing tag, the control is inside. That is exact
    /// for the flat, non-nested way the Control Panel uses these components, and the
    /// render tests next door check the behaviour rather than the markup, so a shape this
    /// misreads still cannot ship silently.</para>
    /// </summary>
    private static bool IsInsideActionGate(string razor, int index)
    {
        var before = razor.AsSpan(0, index);
        var opened = Math.Max(
            before.LastIndexOf("<AuthorizedAction"), before.LastIndexOf("<SimfActionGate"));
        var closed = Math.Max(
            before.LastIndexOf("</AuthorizedAction>"), before.LastIndexOf("</SimfActionGate>"));
        return opened > closed;
    }
}
