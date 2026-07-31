using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>Bi-Meeting rework — the CP page that defines a delegation/country's
/// availability windows (parity with <see cref="SpeakerAvailabilityPage"/>). The
/// picker offers invited countries only; the delegation-meeting flow offers each
/// country's derived free slots. Gated by <c>DelegationMeetings.Manage</c>.</summary>
public partial class DelegationAvailabilityPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminCountrySummary> _countries = new();
    private List<AdminDelegationAvailabilityWindow> _windows = new();
    private int? _countryId;
    private string _start = string.Empty;
    private string _end = string.Empty;
    private string _slotMinutes = "30";
    private bool _busy;
    private Toast? _toast;

    // R10 (D-767) — a must-decide guard for the destructive window delete.
    private bool _confirmOpen;
    private Guid _confirmWindowId;

    // Forum-day bounds read from the backend (MIN/MAX programme day). The string
    // forms feed the datetime-local Min/Max; the DateOnly forms back the client-side
    // range check. All null when no programme days are seeded (no client bound; the
    // server still enforces once days exist).
    private string? _forumMin;
    private string? _forumMax;
    private DateOnly? _forumMinDate;
    private DateOnly? _forumMaxDate;

    protected override async Task OnInitializedAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminCountrySummary>>>(
            "simfAccount.postJson", "/account/api/admin/countries/list",
            new GridQuery { Top = 500 });
        if (envelope is { Success: true, Data: not null })
        {
            // Only invited (وفد) countries can hold delegation meetings, so only they
            // get availability windows — the backend rejects a non-invited country.
            _countries = envelope.Data.Items
                .Where(c => c.IsActive && c.IsInvited)
                .OrderBy(c => c.Name).ToList();
        }

        await LoadForumWindowAsync();
    }

    // Read the forum-day window (MIN/MAX programme day) and translate it into
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

    private async Task OnCountryChanged(ChangeEventArgs e)
    {
        _toast = null;
        _countryId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
        await LoadWindowsAsync();
    }

    private async Task LoadWindowsAsync()
    {
        if (_countryId is not { } id) { _windows = new(); return; }
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminDelegationAvailabilityWindow>>>(
            "simfAccount.getJson", $"/account/api/admin/countries/{id}/availability-windows");
        _windows = envelope is { Success: true, Data: not null }
            ? envelope.Data.ToList()
            : new();
    }

    private async Task AddWindowAsync()
    {
        if (_countryId is not { } id) { return; }
        if (!TryParseUtc(_start, out var start) || !TryParseUtc(_end, out var end))
        {
            _toast = new Toast("error", L["Admin.DelegationAvailability.BadDates"]);
            return;
        }
        // Client-side forum-day check (a backstop to the datetime-local Min/Max and
        // the authoritative server rule). Skipped when no programme days are seeded.
        if (_forumMinDate is { } minDate && _forumMaxDate is { } maxDate)
        {
            var startDate = DateOnly.FromDateTime(start);
            var endDate = DateOnly.FromDateTime(end);
            if (startDate < minDate || endDate > maxDate)
            {
                var range = EventDateRange.Format(
                    minDate, maxDate,
                    CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft);
                _toast = new Toast("error", L["Admin.DelegationAvailability.BadDateRange", range]);
                return;
            }
        }
        if (!int.TryParse(_slotMinutes, out var slot) || slot <= 0)
        {
            _toast = new Toast("error", L["Admin.DelegationAvailability.BadSlot"]);
            return;
        }

        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminDelegationAvailabilityWindow>>(
                "simfAccount.postJson",
                $"/account/api/admin/countries/{id}/availability-windows",
                new CreateDelegationAvailabilityWindowRequest
                {
                    Start = start, End = end, SlotMinutes = slot,
                });
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L["Admin.DelegationAvailability.Added"]);
                _start = _end = string.Empty;
                await LoadWindowsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.DelegationAvailability.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    // R10 (D-767) — open the delete confirm; RunDeleteAsync does the work on OK.
    private void ConfirmDelete(Guid windowId)
    {
        _confirmWindowId = windowId;
        _confirmOpen = true;
        _toast = null;
    }

    private async Task RunDeleteAsync()
    {
        _confirmOpen = false;
        await DeleteWindowAsync(_confirmWindowId);
    }

    private void CancelConfirm() => _confirmOpen = false;

    private async Task DeleteWindowAsync(Guid windowId)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/delegation-availability-windows/{windowId}");
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L["Admin.Availability.WindowDeleted"]);
                await LoadWindowsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.DelegationAvailability.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private static bool TryParseUtc(string value, out DateTime result)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            result = SaudiTime.FromSaudiWallClock(dt);
            return true;
        }
        result = default;
        return false;
    }
}
