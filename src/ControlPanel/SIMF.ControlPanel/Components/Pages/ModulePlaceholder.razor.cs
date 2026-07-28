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

    /// <summary>§6.16 (NAV-011) — the nav label for this slug, or null when the
    /// slug names no module at all.
    ///
    /// <para>The "/m/{Module}" route is a catch-all, and it used to render the
    /// "Coming soon" panel for ANY value: /m/attendees, /m/typo and
    /// /m/does-not-exist each produced a confident, correctly-shelled page
    /// announcing a module that was on its way. A mistyped URL was told the
    /// feature exists but is not built yet, which is a different and worse
    /// answer than "no such page" — the admin waits for something that is never
    /// coming. Only slugs that CpNavigation actually declares as stubs are
    /// real.</para></summary>
    private string? ModuleLabelKey => CpNavigation.LabelKeyForHref($"/m/{Module}");

    private bool IsKnownModule => ModuleLabelKey is not null;

    private string PageTitle =>
        ModuleLabelKey is not null ? L[ModuleLabelKey].Value : L["NotFound.Title"].Value;
}
