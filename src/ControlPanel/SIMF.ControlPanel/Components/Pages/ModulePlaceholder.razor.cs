using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Logs;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Gates;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Faq;

namespace SIMF.ControlPanel.Components.Pages;

public partial class ModulePlaceholder
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    /// <summary>The module slug from the route.</summary>
    [Parameter] public string Module { get; set; } = string.Empty;

    private string PageTitle
    {
        get
        {
            var key = CpNavigation.LabelKeyForHref($"/m/{Module}");
            return key is not null ? L[key].Value : L["Module.ComingSoon.Title"].Value;
        }
    }
}
