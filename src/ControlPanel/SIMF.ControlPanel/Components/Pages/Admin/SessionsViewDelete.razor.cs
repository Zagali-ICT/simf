using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionsViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // A local copy of the row so the lifecycle / recording actions can refresh
    // the displayed detail in place (the parent passes an immutable Initial).
    private AdminSessionDetail? _target;
    private bool _busy;
    private bool _confirming;
    private bool _lifecycleBusy;
    private bool _recordingBusy;
    private string? _error;

    protected override void OnParametersSet() => _target ??= Initial;

    private static string HallLabel(AdminSessionDetail row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.HallNameArabic
            : row.HallName;

    // The resx keys are 1:1 with the enum names (Admin.Sessions.Status.<Name>),
    // so derive the key — no switch + dead fallback to keep in sync.
    private string StatusLabel(SessionStatus status) =>
        L[$"Admin.Sessions.Status.{status}"];

    private static string StatusPillVariant(SessionStatus status) => status switch
    {
        SessionStatus.Published => "on",
        SessionStatus.Scheduled => "neutral",
        _ => "admin",
    };

    private static IEnumerable<(SessionStatus Target, string LabelKey)> NextTransitions(
        SessionStatus current) => current switch
    {
        SessionStatus.Scheduled =>
            [(SessionStatus.Held, "Admin.Sessions.Lifecycle.MarkHeld")],
        SessionStatus.Held =>
            [(SessionStatus.Scheduled, "Admin.Sessions.Lifecycle.BackToScheduled"),
             (SessionStatus.Recorded, "Admin.Sessions.Lifecycle.MarkRecorded")],
        SessionStatus.Recorded =>
            [(SessionStatus.Held, "Admin.Sessions.Lifecycle.BackToHeld"),
             (SessionStatus.Published, "Admin.Sessions.Lifecycle.Publish")],
        SessionStatus.Published =>
            [(SessionStatus.Recorded, "Admin.Sessions.Lifecycle.Unpublish")],
        _ => [],
    };

    private async Task SetStatusAsync(SessionStatus target)
    {
        if (_target is null) { return; }
        _lifecycleBusy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminSessionDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/sessions/{_target.Id}/status",
                new SetSessionStatusRequest { Status = target });
            if (envelope is { Success: true, Data: not null })
            {
                _target = envelope.Data;
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Sessions.Fallback"];
            }
        }
        finally { _lifecycleBusy = false; }
    }

    // Attach / remove the session recording. Upload streams the
    // file through the hidden <input> via simfAccount.uploadFile; the returned
    // detail refreshes the view so HasRecording flips immediately.
    private async Task UploadRecordingAsync()
    {
        if (_target is null) { return; }
        _recordingBusy = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSessionDetail>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/sessions/{_target.Id}/recording",
                "session-recording-input");
            if (env is { Success: true, Data: not null })
            {
                _target = env.Data;
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.Sessions.Fallback"];
            }
        }
        finally { _recordingBusy = false; }
    }

    private async Task DeleteRecordingAsync()
    {
        if (_target is null) { return; }
        _recordingBusy = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSessionDetail>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/sessions/{_target.Id}/recording");
            if (env is { Success: true, Data: not null })
            {
                _target = env.Data;
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.Sessions.Fallback"];
            }
        }
        finally { _recordingBusy = false; }
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is not { } value) { return "—"; }
        return value >= 1_048_576
            ? $"{value / 1_048_576.0:0.#} MB"
            : $"{value / 1024.0:0.#} KB";
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || _target is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/sessions/{_target.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(_target);
            }
            else
            {
                // Close the confirm first so the error lands on the visible form
                // body, not behind the (still-open) confirm overlay.
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Sessions.Fallback"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Sessions.Fallback"];
        }
        finally { _busy = false; }
    }
}
