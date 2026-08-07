using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Logs;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class LogsViewer
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    /// <summary>Used only by the auto-refresh timer, which must swallow whatever it
    /// catches (see <see cref="ResetTimer"/>) — so it logs instead of re-throwing.</summary>
    [Inject] private ILogger<LogsViewer> Logger { get; set; } = default!;

    private const int RefreshIntervalMs = 5_000;

    private LogListResponse? _list;
    private LogTailResponse? _tail;
    private string _selectedProject = string.Empty;
    private string _selectedFile = string.Empty;
    private int _lineCount = 500;
    private bool _autoRefresh = true;
    private bool _loadingList;
    private bool _loadingTail;
    private System.Timers.Timer? _timer;

    /// <summary>§6.16 (F-U5-009) — the load error, when either call fails.
    /// A failed LIST used to render as "no log files" and a failed TAIL used to
    /// blank the pane, so an admin diagnosing an incident was shown "there is
    /// nothing here" when the truth was "I could not ask". With auto-refresh on
    /// a 5-second poll, a transient failure also wiped the text mid-read.</summary>
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await LoadListAsync();
        ResetTimer();
    }

    private async Task LoadListAsync()
    {
        _loadingList = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<LogListResponse>>(
                "simfAccount.getJson", "/account/api/admin/logs/list");
            if (envelope is { Success: true, Data: not null })
            {
                _list = envelope.Data;
                _error = null;
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Logs.LoadFailed"];
                return;
            }
            if (_list.Projects.Count > 0)
            {
                _selectedProject = _list.Projects[0].Name;
                var first = CurrentProjectFiles().FirstOrDefault();
                _selectedFile = first?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(_selectedFile))
                {
                    await LoadTailAsync();
                }
            }
        }
        finally { _loadingList = false; }
    }

    private IReadOnlyList<LogFileEntry> CurrentProjectFiles()
    {
        if (_list is null) return Array.Empty<LogFileEntry>();
        var bucket = _list.Files.FirstOrDefault(f => f.Project == _selectedProject);
        return bucket?.Files ?? Array.Empty<LogFileEntry>();
    }

    private async Task OnProjectChangedAsync(ChangeEventArgs args)
    {
        _selectedProject = args.Value?.ToString() ?? string.Empty;
        var first = CurrentProjectFiles().FirstOrDefault();
        _selectedFile = first?.FileName ?? string.Empty;
        if (!string.IsNullOrEmpty(_selectedFile))
        {
            await LoadTailAsync();
        }
        else
        {
            _tail = null;
        }
    }

    private async Task OnFileChangedAsync(ChangeEventArgs args)
    {
        _selectedFile = args.Value?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(_selectedFile))
        {
            await LoadTailAsync();
        }
    }

    private async Task OnLineCountChangedAsync(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var count))
        {
            _lineCount = count;
            await LoadTailAsync();
        }
    }

    private void OnAutoRefreshChanged(ChangeEventArgs args)
    {
        _autoRefresh = args.Value is bool b && b;
        ResetTimer();
    }

    private async Task RefreshAsync()
    {
        await LoadListAsync();
        if (!string.IsNullOrEmpty(_selectedFile))
        {
            await LoadTailAsync();
        }
    }

    private async Task LoadTailAsync()
    {
        if (string.IsNullOrEmpty(_selectedProject) || string.IsNullOrEmpty(_selectedFile))
        {
            return;
        }
        _loadingTail = true;
        try
        {
            var url = "/account/api/admin/logs/tail"
                + $"?project={Uri.EscapeDataString(_selectedProject)}"
                + $"&file={Uri.EscapeDataString(_selectedFile)}"
                + $"&lines={_lineCount}";
            var envelope = await JS.InvokeAsync<ApiResult<LogTailResponse>>(
                "simfAccount.getJson", url);
            if (envelope is { Success: true, Data: not null })
            {
                _tail = envelope.Data;
                _error = null;
            }
            else
            {
                // Report it, and KEEP the last good content. Blanking the pane
                // on every failed 5-second poll would destroy the text the
                // admin is reading; the banner says the view is stale.
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Logs.TailFailed"];
            }
        }
        finally { _loadingTail = false; }
    }

    private Task DownloadAsync()
    {
        if (string.IsNullOrEmpty(_selectedProject) || string.IsNullOrEmpty(_selectedFile))
        {
            return Task.CompletedTask;
        }
        var url = "/account/api/admin/logs/download"
            + $"?project={Uri.EscapeDataString(_selectedProject)}"
            + $"&file={Uri.EscapeDataString(_selectedFile)}";
        Nav.NavigateTo(url, forceLoad: true);
        return Task.CompletedTask;
    }

    private void ResetTimer()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        if (!_autoRefresh) return;
        _timer = new System.Timers.Timer(RefreshIntervalMs) { AutoReset = true };
        _timer.Elapsed += async (_, _) =>
        {
            // THIS HANDLER MUST NOT THROW.
            //
            // It is an `async void` (the Elapsed delegate returns void), running on
            // a timer thread. Anything that escapes it is an unhandled exception on
            // a thread-pool thread, which TERMINATES THE PROCESS — the whole Control
            // Panel, every signed-in admin, not just this circuit.
            //
            // That is not hypothetical: navigating away from /admin/logs while a
            // 5-second poll was in flight killed the CP server. The disposed circuit
            // cancels the pending JS interop call, LoadTailAsync surfaces
            // TaskCanceledException, and there was no catch between it and the
            // runtime. Found by the WS4 browser sweep, which lost the Control Panel
            // mid-run and only then reported it. ServicesMonitor and SessionLiveHall
            // already guard their PeriodicTimer loops this way; this one did not.
            try
            {
                await InvokeAsync(async () =>
                {
                    if (!_loadingTail && !string.IsNullOrEmpty(_selectedFile))
                    {
                        await LoadTailAsync();
                        StateHasChanged();
                    }
                });
            }
            catch (Exception ex) when (ex is JSDisconnectedException
                or OperationCanceledException or ObjectDisposedException)
            {
                // Circuit torn down mid-poll. Expected, and there is nothing left to
                // refresh — stop rather than keep firing into a dead renderer.
                _timer?.Stop();
            }
            catch (Exception ex)
            {
                // Anything else is a real fault: report it and stop the poll rather
                // than re-throwing into the void every 5 seconds.
                Logger.LogError(ex, "Log tail auto-refresh failed; auto-refresh stopped.");
                _timer?.Stop();
            }
        };
        _timer.Start();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
