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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class LogsViewer
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

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
            await InvokeAsync(async () =>
            {
                if (!_loadingTail && !string.IsNullOrEmpty(_selectedFile))
                {
                    await LoadTailAsync();
                    StateHasChanged();
                }
            });
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
