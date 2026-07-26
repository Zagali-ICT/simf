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
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Faq;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallsViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    // QA B16 — the hall's occupancy view: the sessions assigned to this hall,
    // read from the hall-gated schedule endpoint (which reuses the sessions
    // list's hallId filter). Loaded per hall, so re-opening the shell on a
    // different row re-reads instead of showing the previous hall's sessions.
    private List<AdminSessionSummary> _schedule = new();
    private bool _scheduleLoading;
    private Guid? _scheduleHallId;

    protected override async Task OnParametersSetAsync()
    {
        if (Initial is null || Initial.Id == _scheduleHallId) { return; }
        _scheduleHallId = Initial.Id;
        await LoadScheduleAsync(Initial.Id);
    }

    private async Task LoadScheduleAsync(Guid hallId)
    {
        _scheduleLoading = true;
        _schedule = new();
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
                "simfAccount.getJson", $"/account/api/admin/halls/{hallId}/schedule");
            if (envelope is { Success: true, Data: not null })
            {
                _schedule = envelope.Data.Items.ToList();
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Halls.Schedule.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Halls.Schedule.LoadFailed"];
        }
        finally { _scheduleLoading = false; }
    }

    // Saudi local time, 12-hour — never a raw UTC stamp (D-219).
    private static string Local(DateTimeOffset value) =>
        value.FormatSaudi("dd-MM-yyyy hh:mm tt");

    private static string SessionTitle(AdminSessionSummary session) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? session.TitleArabic
            : session.Title;

    // The resx keys are 1:1 with the enum names (mirrors SessionsList).
    private string StatusLabel(SessionStatus status) =>
        L[$"Admin.Sessions.Status.{status}"];

    private static string StatusPillVariant(SessionStatus status) => status switch
    {
        SessionStatus.Published => "on",
        SessionStatus.Scheduled => "neutral",
        _ => "admin",
    };

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/halls/{Initial.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Halls.Fallback"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Halls.Fallback"];
        }
        finally { _busy = false; }
    }
}
