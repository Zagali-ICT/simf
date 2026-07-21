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
using SIMF.Contracts.Programme;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SpeakerAvailabilityPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSpeakerSummary> _speakers = new();
    private List<AdminSpeakerAvailabilityWindow> _windows = new();
    private Guid? _speakerId;
    private string _start = string.Empty;
    private string _end = string.Empty;
    private string _slotMinutes = "30";
    private bool _busy;
    private Toast? _toast;

    // D-753 — forum-day bounds read from the backend, replacing the former hardcoded
    // 2026-11-23..25 window. The string forms feed the datetime-local Min/Max; the
    // DateOnly forms back the client-side range check. All null when no programme days
    // are seeded (no client bound; the server still enforces once days exist).
    private string? _forumMin;
    private string? _forumMax;
    private DateOnly? _forumMinDate;
    private DateOnly? _forumMaxDate;

    protected override async Task OnInitializedAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSpeakerSummary>>>(
            "simfAccount.postJson", "/account/api/admin/speakers/list",
            new GridQuery { Top = 500 });
        if (envelope is { Success: true, Data: not null })
        {
            _speakers = envelope.Data.Items.OrderBy(s => s.Name).ToList();
        }

        await LoadForumWindowAsync();
    }

    // D-753 — read the forum-day window (MIN/MAX programme day) and translate it into
    // datetime-local Min/Max attributes spanning the whole day. A failed / empty read
    // leaves the bounds null (no client bound); the backend enforces the rule on save.
    private async Task LoadForumWindowAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<ForumWindowResponse>>(
            "simfAccount.getJson", "/account/api/admin/programme/forum-window");
        if (env is { Success: true, Data: not null })
        {
            _forumMinDate = env.Data.MinDate;
            _forumMaxDate = env.Data.MaxDate;
            _forumMin = _forumMinDate is { } min
                ? min.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00"
                : null;
            _forumMax = _forumMaxDate is { } max
                ? max.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T23:59"
                : null;
        }
    }

    private async Task OnSpeakerChanged(ChangeEventArgs e)
    {
        _toast = null;
        _speakerId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        await LoadWindowsAsync();
    }

    private async Task LoadWindowsAsync()
    {
        if (_speakerId is not { } id) { _windows = new(); return; }
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminSpeakerAvailabilityWindow>>>(
            "simfAccount.getJson", $"/account/api/admin/speakers/{id}/availability-windows");
        _windows = envelope is { Success: true, Data: not null }
            ? envelope.Data.ToList()
            : new();
    }

    private async Task AddWindowAsync()
    {
        if (_speakerId is not { } id) { return; }
        if (!TryParseUtc(_start, out var start) || !TryParseUtc(_end, out var end))
        {
            _toast = new Toast("error", L["Admin.SpeakerAvailability.BadDates"]);
            return;
        }
        // D-753 — client-side forum-day check (a backstop to the datetime-local
        // Min/Max and the authoritative server rule). Skipped when no programme days
        // are seeded. Dates compare the entered wall-clock day, matching the whole-day
        // Min/Max attributes.
        if (_forumMinDate is { } minDate && _forumMaxDate is { } maxDate)
        {
            var startDate = DateOnly.FromDateTime(start.UtcDateTime);
            var endDate = DateOnly.FromDateTime(end.UtcDateTime);
            if (startDate < minDate || endDate > maxDate)
            {
                _toast = new Toast("error", L["Admin.SpeakerAvailability.BadDateRange"]);
                return;
            }
        }
        if (!int.TryParse(_slotMinutes, out var slot) || slot <= 0)
        {
            _toast = new Toast("error", L["Admin.SpeakerAvailability.BadSlot"]);
            return;
        }

        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminSpeakerAvailabilityWindow>>(
                "simfAccount.postJson",
                $"/account/api/admin/speakers/{id}/availability-windows",
                new CreateSpeakerAvailabilityWindowRequest
                {
                    StartUtc = start, EndUtc = end, SlotMinutes = slot,
                });
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SpeakerAvailability.Added"]);
                _start = _end = string.Empty;
                await LoadWindowsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerAvailability.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task DeleteWindowAsync(Guid windowId)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/speaker-availability-windows/{windowId}");
            if (envelope is { Success: true })
            {
                await LoadWindowsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerAvailability.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private static bool TryParseUtc(string value, out DateTimeOffset result)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            result = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            return true;
        }
        result = default;
        return false;
    }
}
