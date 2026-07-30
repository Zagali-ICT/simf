using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Gates;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class GateOperatorConsole
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IReadOnlyList<OperatorGateAssignment> _assignments = Array.Empty<OperatorGateAssignment>();
    private Guid _selectedGateId;
    private string _qrInput = string.Empty;
    private bool _busy;
    private GateScanResponse? _last;
    private OperatorDailyReport? _report;

    // The simfAccount JS module is only available once the interactive Blazor
    // connection is up — running these calls in OnInitializedAsync would throw
    // on the SSR prerender pass and surface Blazor's unhandled-error banner.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) { return; }

        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<OperatorGateAssignment>>>(
            "simfAccount.getJson", "/account/api/gates/my-assignments");
        if (envelope is { Success: true, Data: not null })
        {
            _assignments = envelope.Data;
            _selectedGateId = _assignments.FirstOrDefault(a => a.IsActive)?.GateId
                ?? Guid.Empty;
        }
        await LoadReportAsync();
        StateHasChanged();
    }

    private async Task LoadReportAsync()
    {
        var reportEnv = await JS.InvokeAsync<ApiResult<OperatorDailyReport>>(
            "simfAccount.getJson",
            _selectedGateId == Guid.Empty
                ? "/account/api/gates/my-reports/today"
                : $"/account/api/gates/my-reports/today?gateId={_selectedGateId}");
        if (reportEnv is { Success: true, Data: not null })
        {
            _report = reportEnv.Data;
        }
    }

    private async Task OnScanAsync()
    {
        if (_busy || _selectedGateId == Guid.Empty || string.IsNullOrWhiteSpace(_qrInput))
            return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GateScanResponse>>(
                "simfAccount.postJson", $"/account/api/gates/{_selectedGateId}/scans",
                new GateScanRequest
                {
                    Qr = _qrInput.Trim().ToUpperInvariant(),
                    IdempotencyKey = Guid.NewGuid().ToString(),
                    Source = ScanSource.Simulator,
                });
            if (envelope is { Success: true, Data: not null })
            {
                _last = envelope.Data;
                _qrInput = string.Empty;
                await LoadReportAsync();
            }
            else
            {
                _last = new GateScanResponse(0, ScanOutcome.Denied, ScanDirection.CheckIn,
                    DateTimeOffset.UtcNow, null, null,
                    envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.Gates.Fallback"]);
            }
        }
        finally { _busy = false; }
    }
}
