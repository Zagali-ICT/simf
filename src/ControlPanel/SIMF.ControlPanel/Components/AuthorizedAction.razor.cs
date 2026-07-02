using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Notifications;

namespace SIMF.ControlPanel.Components;

public partial class AuthorizedAction
{


    /// <summary>The permission code that gates the content, e.g.
    /// <c>PermissionCatalog.Sessions.Edit</c>.</summary>
    [Parameter, EditorRequired] public string Permission { get; set; } = string.Empty;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string _policy => PermissionCatalog.PolicyFor(Permission);
}
