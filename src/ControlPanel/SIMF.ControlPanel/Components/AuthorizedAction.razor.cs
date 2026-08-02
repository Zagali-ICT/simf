using Microsoft.AspNetCore.Components;
using SIMF.Common;

namespace SIMF.ControlPanel.Components;

public partial class AuthorizedAction
{


    /// <summary>The permission code that gates the content, e.g.
    /// <c>PermissionCatalog.Sessions.Edit</c>.</summary>
    [Parameter, EditorRequired] public string Permission { get; set; } = string.Empty;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string _policy => PermissionCatalog.PolicyFor(Permission);
}
