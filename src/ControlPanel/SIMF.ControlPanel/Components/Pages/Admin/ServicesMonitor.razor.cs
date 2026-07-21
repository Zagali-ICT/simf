using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Ops;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ServicesMonitor : IDisposable
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    private readonly GridQuery _query = new() { Top = 50 };
    private GridPage<WorkerStatus> _page = new();
    private WorkerStatusListResponse? _data;
    private bool _loading;
    private Toast? _toast;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();

        // Poll the snapshot in the background so an operator watching the page sees
        // a worker go stale / faulted without a manual refresh.
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(RefreshInterval);
        _ = AutoRefreshLoopAsync(_cts.Token);
    }

    private async Task AutoRefreshLoopAsync(CancellationToken token)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(token))
            {
                await InvokeAsync(async () =>
                {
                    await LoadAsync();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The page was disposed; stop polling.
        }
        catch (Exception)
        {
            // The render circuit was torn down mid-refresh (LoadAsync handles its
            // own transient failures, so this only fires on teardown); stop polling.
        }
    }

    // The worker list is small and returned whole, so a grid query change simply
    // re-reads the current snapshot rather than paging on the server.
    private Task OnQueryChangedAsync(GridQuery next) => LoadAsync();

    private async Task LoadAsync()
    {
        // Guard against overlapping refreshes (the 15s tick, the Refresh button
        // and a grid query change can all call in).
        if (_loading) { return; }
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<WorkerStatusListResponse>>(
                "simfAccount.getJson", "/account/api/admin/ops/workers");
            if (envelope is { Success: true, Data: not null })
            {
                _data = envelope.Data;
                _page = GridPage<WorkerStatus>.Of(_data.Workers, _data.Workers.Count, _query);
                _toast = null;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ServicesMonitor.LoadFailed"]);
            }
        }
        catch (Exception)
        {
            // A transient transport failure (a rejected fetch) must not kill the
            // background poll; show a toast and let the next tick retry.
            _toast = new Toast("error", L["Admin.ServicesMonitor.LoadFailed"]);
        }
        finally { _loading = false; }
    }

    private string StateVariant(WorkerState state) => state switch
    {
        WorkerState.Healthy => "on",
        WorkerState.Starting => "neutral",
        WorkerState.Stale => "warn",
        WorkerState.Faulted => "danger",
        _ => "neutral",
    };

    private string StateLabel(WorkerState state) => state switch
    {
        WorkerState.Healthy => L["Admin.ServicesMonitor.State.Healthy"],
        WorkerState.Starting => L["Admin.ServicesMonitor.State.Starting"],
        WorkerState.Stale => L["Admin.ServicesMonitor.State.Stale"],
        WorkerState.Faulted => L["Admin.ServicesMonitor.State.Faulted"],
        _ => state.ToString(),
    };

    private static string Local(DateTimeOffset? utc) =>
        utc is { } value ? value.FormatSaudi("yyyy-MM-dd HH:mm:ss") : "-";

    private string RefreshedText() =>
        _data is null
            ? string.Empty
            : string.Format(
                L["Admin.ServicesMonitor.Refreshed"],
                _data.GeneratedUtc.FormatSaudi("HH:mm:ss"));

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.ServicesMonitor.Summary"], total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    public void Dispose()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
    }
}
