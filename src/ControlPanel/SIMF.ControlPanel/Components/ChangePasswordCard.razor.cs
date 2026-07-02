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

public partial class ChangePasswordCard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    private readonly Model _model = new();

    /// <summary>Show the current-password field (Profile = true; forced-change popup = false).</summary>
    [Parameter] public bool ShowCurrentField { get; set; } = true;

    /// <summary>Disables the fields and shows the submit spinner while the host posts.</summary>
    [Parameter] public bool Busy { get; set; }

    /// <summary>Server-side error to surface above the form; null hides the alert.</summary>
    [Parameter] public string? ErrorMessage { get; set; }

    /// <summary>Optional submit-button label; defaults to the Profile change label.</summary>
    [Parameter] public string? SubmitLabel { get; set; }

    /// <summary>Render the submit button full-width (used in the popup).</summary>
    [Parameter] public bool Block { get; set; }

    /// <summary>Raised with the entered values; the host performs the API call.</summary>
    [Parameter] public EventCallback<Model> OnSubmit { get; set; }

    private Task SubmitAsync() => OnSubmit.InvokeAsync(_model);

    /// <summary>The values the host needs to build its request.</summary>
    public sealed class Model
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
