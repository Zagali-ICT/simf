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

public partial class CrudPresentationToggle
{
    [Inject] private CpPreferences Prefs { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    /// <summary>The stable page key the preference is stored under (e.g. "interests").</summary>
    [Parameter, EditorRequired] public string PageKey { get; set; } = string.Empty;

    /// <summary>The current presentation (two-way bound by the host).</summary>
    [Parameter] public CrudPresentation Value { get; set; }

    /// <summary>Raised with the new presentation after the user toggles.</summary>
    [Parameter] public EventCallback<CrudPresentation> ValueChanged { get; set; }

    // The button shows the action it will perform: from Dialog it offers
    // "open as full page"; from Page it offers "open as dialog".
    private string ActionLabel =>
        Value == CrudPresentation.Dialog ? L["Grid.View.Page"] : L["Grid.View.Dialog"];

    private async Task ToggleAsync()
    {
        var next = Value == CrudPresentation.Dialog
            ? CrudPresentation.Page
            : CrudPresentation.Dialog;

        await Prefs.SetPresentationAsync(PageKey, next);
        await ValueChanged.InvokeAsync(next);
    }
}
