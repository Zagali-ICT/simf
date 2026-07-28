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

public partial class CrudGridExcel
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The resource slug, e.g. "interests" — the BFF/API route segment.</summary>
    [Parameter, EditorRequired] public string Resource { get; set; } = default!;

    /// <summary>Raised after a successful import so the page can reload its grid.</summary>
    [Parameter] public EventCallback OnImported { get; set; }

    /// <summary>Raised (with a localized message) when export/import transport fails,
    /// so the page can surface it in its own toast.</summary>
    [Parameter] public EventCallback<string> OnError { get; set; }

    private string InputId => $"{Resource}-import-input";
    private AdminGridImportResult? _result;

    /// <summary>§6.16 (F-U5-008) — true while an import upload is in flight.
    /// Drives the blocking "importing" modal and guards re-entry.</summary>
    private bool _importing;

    /// <summary>Downloads the XLSX for the selected ids, or — when none are
    /// selected — the whole filtered grid (the API caps the row count).</summary>
    public async Task ExportAsync(IReadOnlyList<Guid> ids, GridQuery query)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently. The page
        // already gives us an OnError channel for exactly this.
        var error = await JS.ExportXlsxAsync(
            $"/account/api/admin/{Resource}/export",
            new AdminGridExportRequest
            {
                Ids = ids.ToList(),
                Query = ids.Count == 0 ? query : null,
            }, L);
        if (error is not null && OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    /// <summary>Opens the OS file picker; the chosen .xlsx is uploaded on change.</summary>
    public async Task TriggerImportAsync()
    {
        if (_importing) return;
        await JS.InvokeVoidAsync("simfAccount.triggerFileInput", InputId);
    }

    private async Task OnFileSelectedAsync()
    {
        // §6.16 (F-U5-008) — a spreadsheet import is neither instant nor
        // idempotent. With no busy state the page just sat there looking frozen
        // and the Import button stayed live, so an admin who assumed the first
        // click had missed could import the same file twice and double every
        // created row. Block the surface for the duration and refuse re-entry.
        if (_importing) return;
        _importing = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminGridImportResult>>(
                "simfAccount.uploadFile", $"/account/api/admin/{Resource}/import", InputId);
            if (env is { Success: true, Data: not null })
            {
                _result = env.Data;
                await OnImported.InvokeAsync();
            }
            else if (OnError.HasDelegate)
            {
                await OnError.InvokeAsync(
                    env?.Error?.MessageForCurrentCulture() ?? L["Grid.Import.Failed"].Value);
            }
        }
        finally { _importing = false; }
    }

    private void CloseResult() => _result = null;
}
