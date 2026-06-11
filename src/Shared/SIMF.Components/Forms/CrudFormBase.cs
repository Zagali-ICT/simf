using Microsoft.AspNetCore.Components;

namespace SIMF.Components.Forms;

// D-353 — the shared parameter surface every CRUD form exposes, so the
// CrudShell can host any of them as a popup or a full page and every list
// page wires them the same way. Two concrete bases:
//
//   • CrudAddEditFormBase<T> — one form for Add (IsEdit=false → POST) and
//     Edit (IsEdit=true → PUT), raising OnSuccess with the saved row.
//   • CrudViewDeleteFormBase<T> — one form for read-only View (IsDelete=false)
//     and Delete (IsDelete=true → shows the Delete button + confirmation),
//     raising OnDeleted with the removed row.
//
// Forms inherit a base with `@inherits CrudAddEditFormBase<AdminXSummary>` and
// bind the typed parameters directly from the list page — the host frame never
// touches the form's parameters, so there are no loose dictionaries or
// magic-string parameter names.

/// <summary>Shared base for every CRUD form — carries the row being worked on
/// and the cancel/close callback common to both the add/edit and view/delete
/// forms.</summary>
/// <typeparam name="T">The grid-row summary type the form edits or shows.</typeparam>
public abstract class CrudFormBase<T> : ComponentBase
{
    /// <summary>The row the form is bound to. <c>null</c> in Add mode; the
    /// selected row in Edit, View and Delete modes.</summary>
    [Parameter] public T? Initial { get; set; }

    /// <summary>Raised when the user cancels or closes the form without
    /// committing. The host list page closes the shell in response.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }
}

/// <summary>Base for the combined Add / Edit form (D-353).</summary>
/// <typeparam name="T">The grid-row summary type the form creates or updates.</typeparam>
public abstract class CrudAddEditFormBase<T> : CrudFormBase<T>
{
    /// <summary>True = Edit mode (PUT against <see cref="CrudFormBase{T}.Initial"/>);
    /// false = Add mode (POST a new row).</summary>
    [Parameter] public bool IsEdit { get; set; }

    /// <summary>Raised after a successful create or update, carrying the saved
    /// row. The host closes the shell and reloads the grid in response.</summary>
    [Parameter] public EventCallback<T> OnSuccess { get; set; }
}

/// <summary>Base for the combined View / Delete form (D-353).</summary>
/// <typeparam name="T">The grid-row summary type the form shows or deletes.</typeparam>
public abstract class CrudViewDeleteFormBase<T> : CrudFormBase<T>
{
    /// <summary>True = Delete mode (shows the Delete button and a confirmation
    /// step); false = read-only View mode.</summary>
    [Parameter] public bool IsDelete { get; set; }

    /// <summary>Raised after the row is successfully deleted, carrying the
    /// removed row. The host closes the shell and reloads the grid.</summary>
    [Parameter] public EventCallback<T> OnDeleted { get; set; }
}
