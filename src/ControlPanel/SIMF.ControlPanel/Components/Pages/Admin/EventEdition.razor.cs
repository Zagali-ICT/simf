using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>
/// Event edition (/admin/editions) — the year the forum is running, and the one
/// action that moves it on.
///
/// <para>The action is destructive in a way no other admin page's is: opening a
/// year clears EVERY attendee's badge, and the whole population has to collect a
/// new one. So the primary button opens a confirmation rather than the year, the
/// year is typed rather than incremented, and the resulting count is surfaced —
/// it is the only evidence an operator has that the re-issue actually ran.</para>
/// </summary>
public partial class EventEdition
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private AdminEventEditionResponse? _edition;
    private string _yearInput = string.Empty;
    private bool _busy;
    private bool _confirmOpen;
    private bool _loading;
    private string? _loadError;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _loadError = null;
        try
        {
            // simfReadEnvelope turns a transport failure into a RETURNED
            // ApiResult.Fail rather than a throw, so the catch below never sees
            // the common failure — the envelope has to be checked.
            var env = await JS.InvokeAsync<ApiResult<AdminEventEditionResponse>>(
                "simfAccount.getJson", "/account/api/admin/editions/current");
            if (env is { Success: true, Data: not null })
            {
                _edition = env.Data;
                // Pre-fill the obvious next year. It is still typed and still
                // confirmed — this only saves the common keystroke.
                _yearInput = (_edition.Year + 1).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                _loadError = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Editions.LoadFailed"];
            }
        }
        catch
        {
            _loadError ??= L["Admin.Editions.LoadFailed"];
        }
        finally { _loading = false; }
    }

    /// <summary>Names the consequence rather than a count: the number of badges
    /// cleared is not known until the server has done it, and a guess here would
    /// be worse than the plain statement.</summary>
    private string ConfirmMessage() =>
        string.Format(
            CultureInfo.CurrentUICulture,
            L["Admin.Editions.Open.Confirm.Message"],
            _yearInput,
            _edition?.Year.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private void OpenConfirm()
    {
        if (_busy) { return; }
        // Checked before the dialog rather than after it, so a typo is corrected
        // while the field is still in front of the operator.
        if (!TryReadYear(out _))
        {
            _toast = new Toast("error", L["Admin.Editions.Open.YearInvalid"]);
            return;
        }
        _toast = null;
        _confirmOpen = true;
    }

    private void CloseConfirm() => _confirmOpen = false;

    private bool TryReadYear(out int year) =>
        int.TryParse(
            _yearInput?.Trim(), NumberStyles.None,
            CultureInfo.InvariantCulture, out year)
        && year is >= 2000 and <= 2999;

    private async Task OpenYearAsync()
    {
        if (_busy) { return; }
        if (!TryReadYear(out var year))
        {
            _confirmOpen = false;
            _toast = new Toast("error", L["Admin.Editions.Open.YearInvalid"]);
            return;
        }

        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminOpenEditionResponse>>(
                "simfAccount.postJson", "/account/api/admin/editions/open",
                new AdminOpenEditionRequest { Year = year });
            if (env is { Success: true, Data: not null })
            {
                _confirmOpen = false;
                // The count is the message. An operator asked "did the re-issue
                // run" has nothing else to point at.
                _toast = new Toast("success", string.Format(
                    CultureInfo.CurrentUICulture,
                    L["Admin.Editions.Open.Done"],
                    env.Data.Year,
                    env.Data.BadgesCleared));
                await LoadAsync();
            }
            else
            {
                // The dialog stays OPEN on failure: the server refuses a year
                // that is already open or earlier than the open one, and closing
                // the dialog would hide the correction from the person making it.
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Editions.Open.Failed"]);
            }
        }
        finally { _busy = false; }
    }
}
