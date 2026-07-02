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

    /// <summary>Downloads the XLSX for the selected ids, or — when none are
    /// selected — the whole filtered grid (the API caps the row count).</summary>
    public async Task ExportAsync(IReadOnlyList<Guid> ids, GridQuery query)
    {
        await JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            $"/account/api/admin/{Resource}/export",
            new AdminGridExportRequest
            {
                Ids = ids.ToList(),
                Query = ids.Count == 0 ? query : null,
            });
    }

    /// <summary>Opens the OS file picker; the chosen .xlsx is uploaded on change.</summary>
    public async Task TriggerImportAsync() =>
        await JS.InvokeVoidAsync("simfAccount.triggerFileInput", InputId);

    private async Task OnFileSelectedAsync()
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

    private void CloseResult() => _result = null;
}
