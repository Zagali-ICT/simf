namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>Defaults shared by the admin forms that let an operator choose a
/// colour, so the same brand value is not re-typed as a literal per form.</summary>
internal static class AdminFormDefaults
{
    /// <summary>
    /// SIMF navy — the page colour a new theme or profile type starts on, and the
    /// value the native colour picker falls back to when the stored text is not a
    /// canonical #rrggbb (a CSS variable, or a 3-digit shortcut).
    /// </summary>
    /// <remarks>
    /// Intentionally a literal rather than a theme token: this is seed DATA for an
    /// operator-editable field, not chrome. It is written into the row once and
    /// then owned by the admin, so it must not shift when the theme does.
    /// </remarks>
    internal const string PageColor = "#244A77";
}
