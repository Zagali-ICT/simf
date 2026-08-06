using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>The admin broadcast-Notifications desk. Composes a bilingual
/// message and queues it (in-app + email) to a session's registered attendees or a
/// broad audience; the history grid shows past broadcasts + their delivery status.
/// Calls flow through the same-origin BFF proxy (<c>/account/api/...</c>).</summary>
public partial class Announcements
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private static readonly IReadOnlyList<string> _targetModes = ["Session", "Audience"];
    private static readonly IReadOnlyList<string> _audienceScopes =
        ["ApprovedAppUsers", "EventAttendees", "EveryoneIncludingPending"];
    private static readonly IReadOnlyList<string> _severities =
        ["Info", "Success", "Warning", "Error"];

    // Compose state.
    private string _targetMode = "Session";
    private IReadOnlyList<AdminSessionSummary> _sessions = [];
    private AdminSessionSummary? _selectedSession;
    private string _audienceScope = "ApprovedAppUsers";
    private string _severity = "Info";
    private string _title = string.Empty;
    private string _titleArabic = string.Empty;
    private string _body = string.Empty;
    private string _bodyArabic = string.Empty;
    private int? _estimate;

    // History grid state.
    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminBroadcastSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
        await LoadHistoryAsync();
        await RefreshEstimateAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.postJson", "/account/api/admin/sessions/list",
            // "start", not "startUtc": D-770 renamed the persisted column and
            // AdminSessionService has no arm for the old key, so this asked for a
            // sort nothing served. The picker still came out in start order only
            // because that service's catch-all happens to be OrderBy(Start) — a
            // coincidence, not a contract, and one that would break silently the
            // day that default changes. See D-817.
            new GridQuery { Top = 200, Sort = "start", SortDescending = false });
        if (env is { Success: true, Data: not null })
        {
            _sessions = env.Data.Items;
        }
    }

    private async Task LoadHistoryAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminBroadcastSummary>>>(
                "simfAccount.postJson", "/account/api/admin/notifications/broadcasts/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Announcements.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadHistoryAsync();
    }

    private async Task RefreshEstimateAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<AdminBroadcastEstimateResult>>(
            "simfAccount.postJson", "/account/api/admin/notifications/broadcast/estimate",
            new AdminBroadcastEstimateRequest
            {
                TargetMode = _targetMode,
                SessionId = _targetMode == "Session" ? _selectedSession?.Id : null,
                AudienceScope = _targetMode == "Audience" ? _audienceScope : null,
            });
        _estimate = env is { Success: true, Data: not null }
            ? env.Data.EstimatedRecipients
            : null;
    }

    private async Task OnTargetModeChangedAsync(string? mode)
    {
        _targetMode = mode ?? "Session";
        await RefreshEstimateAsync();
    }

    private async Task OnSessionChangedAsync(AdminSessionSummary? session)
    {
        _selectedSession = session;
        await RefreshEstimateAsync();
    }

    private async Task OnAudienceChangedAsync(string? scope)
    {
        _audienceScope = scope ?? "ApprovedAppUsers";
        await RefreshEstimateAsync();
    }

    private async Task SubmitAsync()
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminBroadcastResult>>(
                "simfAccount.postJson", "/account/api/admin/notifications/broadcast",
                new AdminCreateBroadcastRequest
                {
                    TargetMode = _targetMode,
                    SessionId = _targetMode == "Session" ? _selectedSession?.Id : null,
                    AudienceScope = _targetMode == "Audience" ? _audienceScope : null,
                    Title = _title,
                    TitleArabic = _titleArabic,
                    Body = _body,
                    BodyArabic = _bodyArabic,
                    Severity = _severity,
                });
            if (env is { Success: true, Data: not null })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.Announcements.Sent"], env.Data.EstimatedRecipients));
                // Clear the message; keep the target so a follow-up broadcast is quick.
                _title = _titleArabic = _body = _bodyArabic = string.Empty;
                await LoadHistoryAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Announcements.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private string RecipientCountText() =>
        _estimate is { } count
            ? string.Format(L["Admin.Announcements.Recipients"], count)
            : L["Admin.Announcements.RecipientsUnknown"];

    private string TargetModeLabel(string mode) => mode switch
    {
        "Session" => L["Admin.Announcements.Target.Session"],
        "Audience" => L["Admin.Announcements.Target.Audience"],
        _ => mode,
    };

    private string AudienceLabel(string scope) => scope switch
    {
        "ApprovedAppUsers" => L["Admin.Announcements.Audience.ApprovedAppUsers"],
        "EventAttendees" => L["Admin.Announcements.Audience.EventAttendees"],
        "EveryoneIncludingPending" => L["Admin.Announcements.Audience.EveryoneIncludingPending"],
        _ => scope,
    };

    private string SeverityLabel(string severity) => severity switch
    {
        "Info" => L["Admin.Announcements.Severity.Info"],
        "Success" => L["Admin.Announcements.Severity.Success"],
        "Warning" => L["Admin.Announcements.Severity.Warning"],
        "Error" => L["Admin.Announcements.Severity.Error"],
        _ => severity,
    };

    private string StatusLabel(string status) => status switch
    {
        "Pending" => L["Admin.Announcements.Status.Pending"],
        "Processing" => L["Admin.Announcements.Status.Processing"],
        "Completed" => L["Admin.Announcements.Status.Completed"],
        "Failed" => L["Admin.Announcements.Status.Failed"],
        _ => status,
    };

    // Maps a broadcast status to a SimfPill colour: queued = neutral, sending =
    // accent, sent = green, failed = red.
    private static string StatusPillVariant(string status) => status switch
    {
        "Processing" => "admin",
        "Completed" => "on",
        "Failed" => "danger",
        _ => "neutral",
    };

    private string TargetLabel(AdminBroadcastSummary row)
    {
        if (row.TargetMode == "Session")
        {
            return row.SessionTitle is { Length: > 0 } title
                ? title
                : L["Admin.Announcements.Target.Session"];
        }
        return row.AudienceScope is { Length: > 0 } scope
            ? AudienceLabel(scope)
            : L["Admin.Announcements.Target.Audience"];
    }

    private static string SessionLabel(AdminSessionSummary session) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? $"{session.Code} · {session.TitleArabic}"
            : $"{session.Code} · {session.Title}";

    private static string MessageLabel(AdminBroadcastSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.TitleArabic
            : row.Title;

    // Saudi wall-clock (the CP's datetime convention, D-765), not the server TZ.
    private static string FormatWhen(DateTime when) => when.FormatSaudi();

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);
}
