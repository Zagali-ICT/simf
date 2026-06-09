namespace SIMF.Components.Forms;

/// <summary>How a CRUD add / edit / view form is presented to the user
/// (D-353). This is a UI-only presentation choice — it is NOT a persisted
/// domain enum, so it lives in the component library, not the frozen
/// <c>SIMF.Common.Enums</c> surface (D-110).</summary>
public enum CrudPresentation
{
    /// <summary>The form opens in a centred overlay popup (the default).</summary>
    Dialog = 0,

    /// <summary>The form takes over the content area as a full page; saving or
    /// cancelling returns to the list.</summary>
    Page = 1,
}
