using Microsoft.AspNetCore.Components;

namespace SIMF.ControlPanel.Components;

public partial class AuthorizedAction
{
    /// <summary>The permission code that gates the content, e.g.
    /// <c>PermissionCatalog.Sessions.Edit</c>.</summary>
    [Parameter, EditorRequired] public string Permission { get; set; } = string.Empty;

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
