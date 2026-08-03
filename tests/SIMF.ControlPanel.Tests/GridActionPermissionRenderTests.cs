using Bunit;
using Microsoft.AspNetCore.Components;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// D-830 — the grid's own action gates, asserted by rendering rather than by
/// grepping the markup.
///
/// <para>SimfDataGrid renders its Add / Edit / Delete / Duplicate / Paste / Import /
/// Export / Approve / Reject controls itself, from the callbacks a page wires, so no
/// page could wrap them in a permission gate. 45 Control Panel pages therefore
/// offered those controls to admins the API would refuse. The grid now takes a
/// permission code per callback, and the pages set 179 of them.</para>
///
/// <para>These render the real component under real identities. Three things have to
/// hold, and the first is the one that makes the change safe to ship: a caller that
/// sets NO permission renders exactly as it did before, because every parameter
/// defaults to null. Then each code must reveal its own control and only its own, and
/// a blank code must refuse rather than wave through.</para>
/// </summary>
public sealed class GridActionPermissionRenderTests : CpComponentTestBase
{
    private sealed record Row(Guid Id, string Name);

    private static readonly Row TheRow = new(Guid.NewGuid(), "Row one");

    private const string Add = "button.simf-tbbtn[title='act-add']";
    private const string EditToolbar = "button.simf-tbbtn[title='act-edit']";
    private const string DeleteButton = "button.simf-tbbtn[title='act-delete']";
    private const string Duplicate = "button.simf-tbbtn[title='act-duplicate']";
    private const string Paste = "button.simf-tbbtn[title='act-paste']";
    private const string Import = "button.simf-tbbtn[title='act-import']";
    private const string Export = "button.simf-tbbtn[title='act-export']";
    private const string Approve = "button.simf-tbbtn[title='act-approve']";
    private const string Reject = "button.simf-tbbtn[title='act-reject']";
    private const string Details = "button.simf-tbbtn[title='act-details']";

    /// <summary>Renders a grid with every gated callback wired, and with whichever
    /// permissions the caller names. One row is present so the row-end Delete and Edit
    /// buttons render alongside the toolbar ones; Multiselect is on so the grid is the
    /// realistic shape every CP list page uses, not so any assertion depends on it.</summary>
    private IRenderedComponent<SimfDataGrid<Row>> RenderGrid(
        string? add = null, string? edit = null, string? delete = null,
        string? import = null, string? export = null,
        string? approve = null, string? reject = null)
    {
        var query = new GridQuery();
        return RenderComponent<SimfDataGrid<Row>>(parameters => parameters
            .Add(p => p.Query, query)
            .Add(p => p.Page, GridPage<Row>.Of(new[] { TheRow }, 1, query))
            .Add(p => p.Multiselect, true)
            .Add(p => p.RowKey, row => row.Id.ToString())
            .Add(p => p.OnAdd, EventCallback.Factory.Create(this, () => { }))
            .Add(p => p.OnEditOne, EventCallback.Factory.Create<Row>(this, _ => { }))
            .Add(p => p.OnDeleteOne, EventCallback.Factory.Create<Row>(this, _ => { }))
            .Add(p => p.OnDeleteSelected,
                EventCallback.Factory.Create<IReadOnlyList<Row>>(this, _ => { }))
            .Add(p => p.OnDuplicateOne, EventCallback.Factory.Create<Row>(this, _ => { }))
            .Add(p => p.OnPaste, EventCallback.Factory.Create<string>(this, _ => { }))
            .Add(p => p.OnApproveSelected,
                EventCallback.Factory.Create<IReadOnlyList<Row>>(this, _ => { }))
            .Add(p => p.OnRejectSelected,
                EventCallback.Factory.Create<IReadOnlyList<Row>>(this, _ => { }))
            .Add(p => p.OnImport, EventCallback.Factory.Create(this, () => { }))
            .Add(p => p.OnExport,
                EventCallback.Factory.Create<IReadOnlyList<Row>>(this, _ => { }))
            .Add(p => p.OnDetailsOne, EventCallback.Factory.Create<Row>(this, _ => { }))
            .Add(p => p.AddPermission, add)
            .Add(p => p.EditPermission, edit)
            .Add(p => p.DeletePermission, delete)
            .Add(p => p.ImportPermission, import)
            .Add(p => p.ExportPermission, export)
            .Add(p => p.ApprovePermission, approve)
            .Add(p => p.RejectPermission, reject)
            .Add(p => p.AddLabel, "act-add")
            .Add(p => p.EditLabel, "act-edit")
            .Add(p => p.DeleteLabel, "act-delete")
            .Add(p => p.DuplicateLabel, "act-duplicate")
            .Add(p => p.PasteLabel, "act-paste")
            .Add(p => p.ImportLabel, "act-import")
            .Add(p => p.ExportLabel, "act-export")
            .Add(p => p.ApproveSelectedLabel, "act-approve")
            .Add(p => p.RejectSelectedLabel, "act-reject")
            .Add(p => p.DetailsLabel, "act-details"));
    }

    /// <summary>Every permission the grid takes, all pointing at Countries so one
    /// grant can reveal exactly one control.</summary>
    private IRenderedComponent<SimfDataGrid<Row>> RenderFullyGatedGrid() =>
        RenderGrid(
            add: PermissionCatalog.Countries.Create,
            edit: PermissionCatalog.Countries.Edit,
            delete: PermissionCatalog.Countries.Delete,
            import: PermissionCatalog.Countries.Import,
            export: PermissionCatalog.Countries.Export,
            approve: PermissionCatalog.Visitors.Approve,
            reject: PermissionCatalog.Visitors.Reject);

    [Fact]
    public void A_grid_with_no_permissions_set_renders_every_button()
    {
        // The back-compat guarantee, and the reason this change could touch a
        // component used by every list page in two applications. Null is the default
        // on all seven parameters; a caller that has not opted in gets the pre-D-830
        // grid, whatever the signed-in user holds — here, nothing.
        Grant();

        var cut = RenderGrid();

        Assert.NotEmpty(cut.FindAll(Add));
        Assert.NotEmpty(cut.FindAll(EditToolbar));
        Assert.NotEmpty(cut.FindAll(DeleteButton));
        Assert.NotEmpty(cut.FindAll(Duplicate));
        Assert.NotEmpty(cut.FindAll(Paste));
        Assert.NotEmpty(cut.FindAll(Import));
        Assert.NotEmpty(cut.FindAll(Export));
        Assert.NotEmpty(cut.FindAll(Approve));
        Assert.NotEmpty(cut.FindAll(Reject));
    }

    [Fact]
    public void A_view_only_holder_sees_no_gated_control_but_still_reads()
    {
        Grant(PermissionCatalog.Countries.View);

        var cut = RenderFullyGatedGrid();

        Assert.Empty(cut.FindAll(Add));
        Assert.Empty(cut.FindAll(EditToolbar));
        Assert.Empty(cut.FindAll(DeleteButton));
        Assert.Empty(cut.FindAll(Duplicate));
        Assert.Empty(cut.FindAll(Paste));
        Assert.Empty(cut.FindAll(Import));
        Assert.Empty(cut.FindAll(Export));
        Assert.Empty(cut.FindAll(Approve));
        Assert.Empty(cut.FindAll(Reject));

        // Reading is what the page's own View permission bought, and it survives.
        // Without this the test would pass just as well against a grid that failed
        // to render at all.
        Assert.NotEmpty(cut.FindAll(Details));
        Assert.NotEmpty(cut.FindAll("tr.simf-grid__row"));
    }

    [Fact]
    public void Each_code_reveals_its_own_control_and_no_other()
    {
        Grant(PermissionCatalog.Countries.View, PermissionCatalog.Countries.Create);

        var cut = RenderFullyGatedGrid();

        Assert.NotEmpty(cut.FindAll(Add));
        Assert.Empty(cut.FindAll(EditToolbar));
        Assert.Empty(cut.FindAll(DeleteButton));
        Assert.Empty(cut.FindAll(Import));
        Assert.Empty(cut.FindAll(Export));
    }

    [Fact]
    public void Create_covers_Add_Duplicate_and_Paste_together()
    {
        // The three collapsed onto one code. Duplicating a row and pasting rows both
        // create, the catalogue has no Duplicate or Paste code, and the API gates
        // POST /admin/admins/duplicate on the same Admins.Create as plain create.
        Grant(PermissionCatalog.Countries.View, PermissionCatalog.Countries.Create);

        var cut = RenderFullyGatedGrid();

        Assert.NotEmpty(cut.FindAll(Add));
        Assert.NotEmpty(cut.FindAll(Duplicate));
        Assert.NotEmpty(cut.FindAll(Paste));
    }

    [Fact]
    public void Export_is_gated_on_its_own_code_not_on_View()
    {
        // Export leaves the building with a spreadsheet of the whole result set, and
        // all 44 pages that offer it have their own X.Export code on the endpoint
        // behind it. Treating it as "reading, which View covers" would have left 44
        // buttons that 403 on press.
        Grant(PermissionCatalog.Countries.View, PermissionCatalog.Countries.Export);

        var cut = RenderFullyGatedGrid();

        Assert.NotEmpty(cut.FindAll(Export));
        Assert.Empty(cut.FindAll(Import));
        Assert.Empty(cut.FindAll(Add));
    }

    [Fact]
    public void Delete_reveals_both_the_bulk_and_the_row_control()
    {
        // One code drives two render sites — the toolbar's "delete selected" and the
        // per-row bin. A gate on only one of them would leave the other as the way in,
        // so both are asserted.
        Grant(PermissionCatalog.Countries.View, PermissionCatalog.Countries.Delete);

        var cut = RenderFullyGatedGrid();

        Assert.Equal(2, cut.FindAll(DeleteButton).Count);
        Assert.Empty(cut.FindAll(Add));
    }

    [Fact]
    public void Edit_and_delete_are_gated_in_the_context_menu_too()
    {
        // The third render site, and the one a markup scan is least likely to notice:
        // right-clicking a row offers Edit, Duplicate and Delete from a menu the grid
        // builds from the same callbacks.
        Grant(PermissionCatalog.Countries.View);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderFullyGatedGrid();
        cut.Find("tr.simf-grid__row").ContextMenu();

        Assert.DoesNotContain("act-edit", cut.Markup);
        Assert.DoesNotContain("act-delete", cut.Markup);
        Assert.DoesNotContain("act-duplicate", cut.Markup);
    }

    [Fact]
    public void An_empty_permission_refuses_rather_than_waving_through()
    {
        // Null means ungated; empty does NOT. PolicyFor("") names no registered
        // policy, so it denies — the behaviour AuthorizedAction has always had. A
        // blank code arriving from a mistyped constant must never widen access.
        Grant(PermissionCatalog.Countries.View, PermissionCatalog.Countries.Create);

        var cut = RenderGrid(add: string.Empty);

        Assert.Empty(cut.FindAll(Add));
    }

    [Fact]
    public void The_countries_page_gates_the_grid_it_hosts()
    {
        // One page rendered end-to-end, so the parameters are proved to be wired and
        // not merely declared.
        Grant(PermissionCatalog.Countries.View);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<CountriesList>();

        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Admin.Countries.Action.Add']"));
        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Grid.Import']"));
        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Grid.Export']"));
        // The page itself rendered — so the absences above are the gate.
        Assert.Contains("simf-grid", cut.Markup);
    }

    [Fact]
    public void Users_edit_is_gated_on_AssignRoles_because_that_is_what_it_calls()
    {
        // The mapping that a naming convention would have got wrong. There is no
        // Admins.Edit code and no PUT /admin/admins/{id}: the grid's Edit opens the
        // roles form, which calls PUT /admin/admins/{id}/roles — gated on
        // Admins.AssignRoles. Gating the button on anything else would either hide it
        // from someone entitled to it or show it to someone who is not.
        Grant(PermissionCatalog.Admins.View, PermissionCatalog.Admins.AssignRoles);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<UsersList>();

        Assert.NotEmpty(cut.FindAll("button.simf-tbbtn[title='Admin.Users.Action.Edit']"));
        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Admin.Users.Action.Add']"));
    }

    [Fact]
    public void Users_edit_is_hidden_without_AssignRoles()
    {
        Grant(PermissionCatalog.Admins.View);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<UsersList>();

        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Admin.Users.Action.Edit']"));
    }

    // D-831 — SimfConfirm renders its own Confirm button, the same structural gap.

    private IRenderedComponent<SimfConfirm> RenderConfirm(string? permission)
    {
        // SimfConfirm wraps SimfModal, which binds a focus trap through JS on render.
        JSInterop.Mode = JSRuntimeMode.Loose;
        return RenderComponent<SimfConfirm>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "confirm-title")
            .Add(p => p.Message, "confirm-message")
            .Add(p => p.ConfirmLabel, "act-commit")
            .Add(p => p.CancelLabel, "act-cancel")
            .Add(p => p.Permission, permission)
            .Add(p => p.OnConfirm, EventCallback.Factory.Create(this, () => { })));
    }

    [Fact]
    public void A_confirm_dialog_with_no_permission_renders_its_commit_button()
    {
        // Back-compat: 30 dialogs live inside form components reached only through a
        // trigger that is already gated, and they set nothing. They must be unchanged.
        Grant();

        var cut = RenderConfirm(permission: null);

        Assert.Contains("act-commit", cut.Markup);
    }

    [Fact]
    public void A_confirm_dialog_hides_its_commit_button_but_never_its_cancel()
    {
        // The half that matters as much as hiding Confirm: a holder who may not commit
        // must still be able to close a dialog that opened on them. Gating Cancel would
        // trap them in a modal with RequireExplicitClose.
        Grant(PermissionCatalog.Faq.View);

        var cut = RenderConfirm(PermissionCatalog.Faq.Delete);

        Assert.DoesNotContain("act-commit", cut.Markup);
        Assert.Contains("act-cancel", cut.Markup);
        Assert.Contains("confirm-message", cut.Markup);
    }

    [Fact]
    public void A_confirm_dialog_shows_its_commit_button_to_a_holder()
    {
        Grant(PermissionCatalog.Faq.View, PermissionCatalog.Faq.Delete);

        var cut = RenderConfirm(PermissionCatalog.Faq.Delete);

        Assert.Contains("act-commit", cut.Markup);
    }

    [Fact]
    public void Gates_import_is_gated_separately_from_the_pages_own_Manage_code()
    {
        // The defect the first cut of the ratchet could not see, because it only
        // looked at View-gated pages. /admin/gates is gated on Gates.Manage, and its
        // Import calls an endpoint gated on the SEPARATE code Gates.Import — so a
        // Manage holder without Import got the button and a 403.
        Grant(PermissionCatalog.Gates.Manage);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<GatesList>();

        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Grid.Import']"));
        Assert.Empty(cut.FindAll("button.simf-tbbtn[title='Grid.Export']"));
        // Manage does still reveal the CRUD controls it really does gate.
        Assert.NotEmpty(cut.FindAll("button.simf-tbbtn[title='Admin.Gates.Action.Add']"));
    }
}
