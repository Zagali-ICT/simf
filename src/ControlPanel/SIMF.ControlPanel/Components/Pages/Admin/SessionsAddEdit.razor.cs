using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _hallIdInput = string.Empty;
    private string _categoryIdInput = string.Empty;
    private string _typeInput = string.Empty;
    // D-485 — per-session seat-mode override ("" = inherit, "0" = Assigned, "1" = Open).
    private string _seatModeInput = string.Empty;
    private string _startInput = string.Empty;
    private string _endInput = string.Empty;
    private string _capacityInput = string.Empty;
    // D-839 — blank means "inherit the hall" (which may itself inherit the global
    // value); the placeholder shows what that currently resolves to.
    private string _arrivalGraceInput = string.Empty;
    private string _speakerPickInput = string.Empty;
    private string _themePickInput = string.Empty;
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // A1 — a hall move or a start/end change cascade-releases EVERY seat already
    // held for the session. The admin used to get no warning at all and only the
    // generic "was updated" toast afterwards, so nudging an end time by half an hour
    // silently wiped the room. _confirmRelease opens the must-decide dialog naming
    // the real count; _releaseAccepted records that the admin said yes, so the
    // second pass through HandleSubmitAsync actually submits.
    private bool _confirmRelease;
    private bool _releaseAccepted;

    /// <summary>A1/A6 — how many active attendee registrations the pending change
    /// would release (stamped on the loaded detail by the admin API).</summary>
    private int ReleasedReservations => Initial?.ActiveReservationCount ?? 0;

    /// <summary>A6 — how many admin-reserved row blocks the pending change would
    /// destroy. They carry no attendee, so nothing else reports them.</summary>
    private int ReleasedAdminBlocks => Initial?.ActiveAdminBlockCount ?? 0;

    private string[] _hallIds = Array.Empty<string>();
    private Dictionary<string, AdminHallSummary> _hallsById = new();
    private string[] _speakerOptions = Array.Empty<string>();
    private Dictionary<string, AdminSpeakerSummary> _speakersById = new();
    private string[] _themeOptions = Array.Empty<string>();
    private Dictionary<string, AdminThemeSummary> _themesById = new();
    private string[] _categoryIds = Array.Empty<string>();
    private Dictionary<string, AdminSessionCategorySummary> _categoriesById = new();

    // D-452 — fixed session-type options (Workshop=0 / Session=1 / Event=2).
    private static readonly string[] _typeOptions = { "0", "1", "2" };

    // D-485 — seat-mode override options ("0" = AssignedSeat, "1" = OpenSeating);
    // the empty placeholder selection inherits the hall's mode.
    private static readonly string[] _seatModeOptions = { "0", "1" };

    private readonly List<AdminSessionSpeakerEntry> _selectedSpeakers = new();
    private readonly List<Guid> _selectedThemes = new();

    // Website Session-detail "أبرز المخرجات" — mutable editor rows for the
    // session's key-outcome bullets (the contract entry is an immutable record,
    // so the form binds against these mutable inputs and maps on submit).
    private readonly List<OutcomeInput> _outcomes = new();

    private sealed class OutcomeInput
    {
        public string Text { get; set; } = string.Empty;
        public string TextArabic { get; set; } = string.Empty;
    }

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.Title = Initial.Title;
            _model.TitleArabic = Initial.TitleArabic;
            _model.Description = Initial.Description ?? string.Empty;
            _model.DescriptionArabic = Initial.DescriptionArabic ?? string.Empty;
            _model.Language = Initial.Language ?? string.Empty;
            _model.LanguageArabic = Initial.LanguageArabic ?? string.Empty;
            _model.LiveStreamUrl = Initial.LiveStreamUrl ?? string.Empty;
            _model.LiveSignLanguageUrl = Initial.LiveSignLanguageUrl ?? string.Empty;
            _model.LiveNotice = Initial.LiveNotice ?? string.Empty;
            _model.LiveNoticeArabic = Initial.LiveNoticeArabic ?? string.Empty;
            _model.LiveCaptions = Initial.LiveCaptions ?? string.Empty;
            _model.LiveCaptionsArabic = Initial.LiveCaptionsArabic ?? string.Empty;
            _model.IsActive = Initial.IsActive;
            _hallIdInput = Initial.HallId.ToString();
            _categoryIdInput = Initial.CategoryId?.ToString() ?? string.Empty;
            _typeInput = Initial.Type.HasValue
                ? ((int)Initial.Type.Value).ToString()
                : string.Empty;
            _seatModeInput = Initial.SeatSelectionModeOverride.HasValue
                ? ((int)Initial.SeatSelectionModeOverride.Value).ToString()
                : string.Empty;
            _startInput = Initial.Start.ToString("yyyy-MM-ddTHH:mm");
            _endInput = Initial.End.ToString("yyyy-MM-ddTHH:mm");
            _capacityInput = Initial.CapacityOverride?.ToString() ?? string.Empty;
            _arrivalGraceInput = Initial.ArrivalGraceMinutesOverride?.ToString() ?? string.Empty;
            _selectedSpeakers.AddRange(Initial.Speakers);
            _selectedThemes.AddRange(Initial.ThemeIds);
            if (Initial.Outcomes is not null)
            {
                foreach (var outcome in Initial.Outcomes.OrderBy(o => o.DisplayOrder))
                {
                    _outcomes.Add(new OutcomeInput
                    {
                        Text = outcome.Text,
                        TextArabic = outcome.TextArabic,
                    });
                }
            }
        }
        _editContext = new EditContext(_model);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await LoadHallsAsync();
        await LoadSpeakersAsync();
        await LoadThemesAsync();
        await LoadCategoriesAsync();
        StateHasChanged();
    }

    private async Task LoadHallsAsync()
    {
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
                "simfAccount.postJson", "/account/api/admin/halls/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (envelope is { Success: true, Data: not null })
            {
                _hallsById = envelope.Data.Items.ToDictionary(h => h.Id.ToString(), h => h);
                _hallIds = envelope.Data.Items
                    .OrderBy(h => h.Name)
                    .Select(h => h.Id.ToString())
                    .ToArray();
            }
            else
            {
                // §6.16 (F-U5-003) — the commoner failure is a RETURNED failed
                // envelope, not a throw: simfReadEnvelope converts an HTTP error
                // or a non-JSON error page into ApiResult.Fail instead of
                // throwing, so the catch above never sees it.
                ReportLookupFailure();
            }
        }
        catch
        {
            // §6.16 (F-U5-003) — a bare `catch { }` left the Hall / Speaker /
            // Theme / Category picker silently EMPTY. HandleSubmitAsync then
            // hard-fails on the missing hall, so the form cannot be saved and
            // nothing on screen says why. The dialog already renders _error.
            ReportLookupFailure();
        }
    }

    private async Task LoadSpeakersAsync()
    {
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSpeakerSummary>>>(
                "simfAccount.postJson", "/account/api/admin/speakers/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (envelope is { Success: true, Data: not null })
            {
                _speakersById = envelope.Data.Items.ToDictionary(s => s.Id.ToString(), s => s);
                RefreshSpeakerOptions();
            }
            else
            {
                // §6.16 (F-U5-003) — the commoner failure is a RETURNED failed
                // envelope, not a throw: simfReadEnvelope converts an HTTP error
                // or a non-JSON error page into ApiResult.Fail instead of
                // throwing, so the catch above never sees it.
                ReportLookupFailure();
            }
        }
        catch
        {
            // §6.16 (F-U5-003) — a bare `catch { }` left the Hall / Speaker /
            // Theme / Category picker silently EMPTY. HandleSubmitAsync then
            // hard-fails on the missing hall, so the form cannot be saved and
            // nothing on screen says why. The dialog already renders _error.
            ReportLookupFailure();
        }
    }

    private async Task LoadThemesAsync()
    {
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminThemeSummary>>>(
                "simfAccount.postJson", "/account/api/admin/themes/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (envelope is { Success: true, Data: not null })
            {
                _themesById = envelope.Data.Items.ToDictionary(t => t.Id.ToString(), t => t);
                RefreshThemeOptions();
            }
            else
            {
                // §6.16 (F-U5-003) — the commoner failure is a RETURNED failed
                // envelope, not a throw: simfReadEnvelope converts an HTTP error
                // or a non-JSON error page into ApiResult.Fail instead of
                // throwing, so the catch above never sees it.
                ReportLookupFailure();
            }
        }
        catch
        {
            // §6.16 (F-U5-003) — a bare `catch { }` left the Hall / Speaker /
            // Theme / Category picker silently EMPTY. HandleSubmitAsync then
            // hard-fails on the missing hall, so the form cannot be saved and
            // nothing on screen says why. The dialog already renders _error.
            ReportLookupFailure();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionCategorySummary>>>(
                "simfAccount.postJson", "/account/api/admin/session-categories/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (envelope is { Success: true, Data: not null })
            {
                _categoriesById = envelope.Data.Items.ToDictionary(c => c.Id.ToString(), c => c);
                _categoryIds = envelope.Data.Items
                    .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                    .Select(c => c.Id.ToString())
                    .ToArray();
            }
            else
            {
                // §6.16 (F-U5-003) — the commoner failure is a RETURNED failed
                // envelope, not a throw: simfReadEnvelope converts an HTTP error
                // or a non-JSON error page into ApiResult.Fail instead of
                // throwing, so the catch above never sees it.
                ReportLookupFailure();
            }
        }
        catch
        {
            // §6.16 (F-U5-003) — a bare `catch { }` left the Hall / Speaker /
            // Theme / Category picker silently EMPTY. HandleSubmitAsync then
            // hard-fails on the missing hall, so the form cannot be saved and
            // nothing on screen says why. The dialog already renders _error.
            ReportLookupFailure();
        }
    }

    private string CategoryLabel(string id)
    {
        if (!_categoriesById.TryGetValue(id, out var c)) return id;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? c.NameArabic
            : c.Name;
    }

    // D-452 — localized label for a session-type option id ("0"/"1"/"2").
    private string TypeLabel(string id) => id switch
    {
        "0" => L["Admin.Sessions.Type.Workshop"],
        "1" => L["Admin.Sessions.Type.Session"],
        "2" => L["Admin.Sessions.Type.Event"],
        _ => id,
    };

    // D-452 — empty selection = unset; otherwise the parsed SessionType.
    private static SessionType? ParseType(string raw) =>
        int.TryParse(raw, out var n) && Enum.IsDefined(typeof(SessionType), n)
            ? (SessionType)n
            : null;

    // D-485 — the per-session seat-mode override select ("0"/"1"); empty = inherit.
    private string SeatModeLabel(string id) => id switch
    {
        "1" => L["Admin.Sessions.SeatMode.Open"],
        _ => L["Admin.Sessions.SeatMode.Assigned"],
    };

    private SeatSelectionMode? ParseSeatModeOverride() =>
        int.TryParse(_seatModeInput, out var n)
            && Enum.IsDefined(typeof(SeatSelectionMode), n)
            ? (SeatSelectionMode)n
            : null;

    private void RefreshSpeakerOptions()
    {
        var picked = _selectedSpeakers.Select(s => s.SpeakerId.ToString()).ToHashSet();
        _speakerOptions = _speakersById.Keys
            .Where(id => !picked.Contains(id))
            .OrderBy(id => SpeakerLabel(id))
            .ToArray();
    }

    private void RefreshThemeOptions()
    {
        var picked = _selectedThemes.Select(id => id.ToString()).ToHashSet();
        _themeOptions = _themesById.Keys
            .Where(id => !picked.Contains(id))
            .OrderBy(id => ThemeLabel(id))
            .ToArray();
    }

    private string HallLabel(string id)
    {
        if (!_hallsById.TryGetValue(id, out var hall)) return id;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? $"{hall.NameArabic} ({hall.Code})"
            : $"{hall.Name} ({hall.Code})";
    }

    private string SpeakerLabel(string id)
    {
        if (!_speakersById.TryGetValue(id, out var s)) return id;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? s.NameArabic
            : s.Name;
    }

    private string ThemeLabel(string id)
    {
        if (!_themesById.TryGetValue(id, out var t)) return id;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? t.NameArabic
            : t.Name;
    }

    private Task OnAddSpeakerAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var speakerId))
        {
            return Task.CompletedTask;
        }
        if (_selectedSpeakers.Any(e => e.SpeakerId == speakerId))
        {
            return Task.CompletedTask;
        }
        var label = _speakersById.TryGetValue(id, out var speaker) ? speaker : null;
        _selectedSpeakers.Add(new AdminSessionSpeakerEntry(
            speakerId,
            speaker?.Name ?? string.Empty,
            speaker?.NameArabic ?? string.Empty,
            _selectedSpeakers.Count));
        _speakerPickInput = string.Empty;
        RefreshSpeakerOptions();
        return Task.CompletedTask;
    }

    private void MoveSpeakerUp(int index)
    {
        if (index <= 0) return;
        (_selectedSpeakers[index - 1], _selectedSpeakers[index]) =
            (_selectedSpeakers[index], _selectedSpeakers[index - 1]);
        RenumberSpeakers();
    }

    private void MoveSpeakerDown(int index)
    {
        if (index >= _selectedSpeakers.Count - 1) return;
        (_selectedSpeakers[index + 1], _selectedSpeakers[index]) =
            (_selectedSpeakers[index], _selectedSpeakers[index + 1]);
        RenumberSpeakers();
    }

    private void RemoveSpeaker(int index)
    {
        _selectedSpeakers.RemoveAt(index);
        RenumberSpeakers();
        RefreshSpeakerOptions();
    }

    // B9 — D-225: set a speaker's per-session role (speaker / host).
    private void OnSpeakerRoleChanged(int index, ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var raw)
            && Enum.IsDefined(typeof(SessionSpeakerRole), raw))
        {
            _selectedSpeakers[index] =
                _selectedSpeakers[index] with { Role = (SessionSpeakerRole)raw };
        }
    }

    private void RenumberSpeakers()
    {
        for (var i = 0; i < _selectedSpeakers.Count; i++)
        {
            _selectedSpeakers[i] = _selectedSpeakers[i] with { DisplayOrder = i };
        }
    }

    // Website Session-detail "أبرز المخرجات" — the outcome-list editor. No
    // DisplayOrder to renumber (the list position IS the order; the service
    // renumbers 0..n-1 on save).
    private void AddOutcome() => _outcomes.Add(new OutcomeInput());

    private void MoveOutcomeUp(int index)
    {
        if (index <= 0) return;
        (_outcomes[index - 1], _outcomes[index]) = (_outcomes[index], _outcomes[index - 1]);
    }

    private void MoveOutcomeDown(int index)
    {
        if (index >= _outcomes.Count - 1) return;
        (_outcomes[index + 1], _outcomes[index]) = (_outcomes[index], _outcomes[index + 1]);
    }

    private void RemoveOutcome(int index) => _outcomes.RemoveAt(index);

    // Map the editor rows to the request. An entirely-blank row is dropped; a
    // partially-filled row flows through so the API returns a clean 400 telling
    // the editor to complete both languages. DisplayOrder = final list position.
    private List<AdminSessionOutcomeEntry> BuildOutcomes()
    {
        var result = new List<AdminSessionOutcomeEntry>();
        foreach (var outcome in _outcomes)
        {
            if (string.IsNullOrWhiteSpace(outcome.Text)
                && string.IsNullOrWhiteSpace(outcome.TextArabic))
            {
                continue;
            }
            result.Add(new AdminSessionOutcomeEntry(
                outcome.Text.Trim(), outcome.TextArabic.Trim(), result.Count));
        }
        return result;
    }

    private Task OnAddThemeAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var themeId))
        {
            return Task.CompletedTask;
        }
        if (_selectedThemes.Contains(themeId)) return Task.CompletedTask;
        _selectedThemes.Add(themeId);
        _themePickInput = string.Empty;
        RefreshThemeOptions();
        return Task.CompletedTask;
    }

    private void RemoveTheme(Guid id)
    {
        _selectedThemes.Remove(id);
        RefreshThemeOptions();
    }

    // #3 / #4 — a lightweight "required" marker for the Type + Speakers fields.
    // Appended at render time so the underlying resx label stays reusable elsewhere.
    private string RequiredLabel(string key) => $"{L[key]} *";

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        var form = ReadForm();
        if (form is null) { return; }

        // A1 / A6 — the API cascade-releases every held seat when the hall or the
        // start/end window moves. Mirror its exact trigger (same field comparison)
        // so the dialog appears precisely when seats really would be destroyed, and
        // never on an edit that leaves the slot alone.
        if (WouldReleaseHeldSeats(form.HallId, form.Start, form.End) && !_releaseAccepted)
        {
            _confirmRelease = true;
            return;
        }

        _busy = true;
        try
        {
            var result = await SendAsync(
                JS,
                "/account/api/admin/sessions",
                $"/account/api/admin/sessions/{Initial?.Id}",
                BuildCreateRequest(form),
                BuildUpdateRequest(form));

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.Sessions.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    /// <summary>Validates the form and returns its parsed values, or null after
    /// setting <see cref="_error"/> to the first problem found.</summary>
    private FormValues? ReadForm()
    {
        if (string.IsNullOrWhiteSpace(_model.Code) || _model.Code.Length is < 2 or > 16)
        {
            _error = L["Admin.Sessions.Field.CodeInvalid"];
            return null;
        }
        if (string.IsNullOrWhiteSpace(_model.Title) || _model.Title.Length > 256)
        {
            _error = L["Admin.Sessions.Field.TitleInvalid"];
            return null;
        }
        if (string.IsNullOrWhiteSpace(_model.TitleArabic) || _model.TitleArabic.Length > 256)
        {
            _error = L["Admin.Sessions.Field.TitleArabicInvalid"];
            return null;
        }
        if (!Guid.TryParse(_hallIdInput, out var hallId))
        {
            _error = L["Admin.Sessions.Field.HallRequired"];
            return null;
        }
        if (!DateTime.TryParse(_startInput, out var startLocal)
            || !DateTime.TryParse(_endInput, out var endLocal))
        {
            _error = L["Admin.Sessions.Field.TimeInvalid"];
            return null;
        }
        var start = SaudiTime.FromSaudiWallClock(startLocal);
        var end = SaudiTime.FromSaudiWallClock(endLocal);
        if (end <= start)
        {
            _error = L["Admin.Sessions.Field.TimeWindowInvalid"];
            return null;
        }
        int? capacityOverride = null;
        if (!string.IsNullOrWhiteSpace(_capacityInput))
        {
            if (!int.TryParse(_capacityInput, out var parsed) || parsed < 0)
            {
                _error = L["Admin.Sessions.Field.CapacityInvalid"];
                return null;
            }
            capacityOverride = parsed;
        }
        // D-839 — blank = inherit the hall; otherwise a whole number inside the
        // shared bound. Same rule object the server validates with.
        if (!WalkInModeOptions.TryParseArrivalGrace(
                _arrivalGraceInput, out var arrivalGraceOverride))
        {
            _error = L["Admin.Sessions.Field.ArrivalGraceInvalid"];
            return null;
        }
        // §8 / D-349 — each non-blank live URL must be a YouTube link or an HLS/MP4
        // stream. Shared rule (LiveStreamUrlPolicy); the API enforces the same.
        var liveUrlInvalid =
            (!string.IsNullOrWhiteSpace(_model.LiveStreamUrl)
                && !LiveStreamUrlPolicy.IsAllowed(_model.LiveStreamUrl))
            || (!string.IsNullOrWhiteSpace(_model.LiveSignLanguageUrl)
                && !LiveStreamUrlPolicy.IsAllowed(_model.LiveSignLanguageUrl));
        if (liveUrlInvalid)
        {
            _error = L["Admin.Sessions.Field.LiveUrlInvalid"];
            return null;
        }
        // B9b — D-226: optional category (empty selection = no category).
        Guid? categoryId = Guid.TryParse(_categoryIdInput, out var cid) ? cid : null;

        var type = ParseType(_typeInput);
        return ValidateTypeAndSpeakers(type)
            ? new FormValues(hallId, start, end, capacityOverride, categoryId, type, arrivalGraceOverride)
            : null;
    }

    /// <summary>
    /// #3 / #4 — mirrors the API's two rules with the same no-regression
    /// grandfather (<c>Initial</c> holds the stored row on edit): a new session
    /// must declare a type and, unless it is an Event, carry at least one
    /// speaker. On edit a legacy violating row stays saveable, but a compliant
    /// row cannot be regressed (clear a set type / drop the last speaker of a
    /// non-Event).
    /// </summary>
    private bool ValidateTypeAndSpeakers(SessionType? type)
    {
        static bool SpeakerRuleMet(SessionType? t, int count) =>
            t == SessionType.Event || count >= 1;

        if ((!IsEdit || Initial?.Type is not null) && type is null)
        {
            _error = L["Admin.Sessions.Field.TypeRequired"];
            return false;
        }
        var speakerRuleApplied = !IsEdit
            || SpeakerRuleMet(Initial?.Type, Initial?.Speakers.Count ?? 0);
        if (speakerRuleApplied && !SpeakerRuleMet(type, _selectedSpeakers.Count))
        {
            _error = L["Admin.Sessions.Field.SpeakerRequired"];
            return false;
        }
        return true;
    }

    private AdminCreateSessionRequest BuildCreateRequest(FormValues form) => new()
    {
        Code = _model.Code.Trim().ToUpperInvariant(),
        Title = _model.Title.Trim(),
        TitleArabic = _model.TitleArabic.Trim(),
        Description = NullIfBlank(_model.Description),
        DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
        HallId = form.HallId,
        CategoryId = form.CategoryId,
        Type = form.Type,
        Start = form.Start,
        End = form.End,
        CapacityOverride = form.CapacityOverride,
        Speakers = _selectedSpeakers.ToList(),
        ThemeIds = _selectedThemes.ToList(),
        LiveStreamUrl = NullIfBlank(_model.LiveStreamUrl),
        LiveSignLanguageUrl = NullIfBlank(_model.LiveSignLanguageUrl),
        LiveNotice = NullIfBlank(_model.LiveNotice),
        LiveNoticeArabic = NullIfBlank(_model.LiveNoticeArabic),
        LiveCaptions = NullIfBlank(_model.LiveCaptions),
        LiveCaptionsArabic = NullIfBlank(_model.LiveCaptionsArabic),
        SeatSelectionModeOverride = ParseSeatModeOverride(),
        ArrivalGraceMinutesOverride = form.ArrivalGraceMinutesOverride,
        Language = NullIfBlank(_model.Language),
        LanguageArabic = NullIfBlank(_model.LanguageArabic),
        Outcomes = BuildOutcomes(),
    };

    private AdminUpdateSessionRequest BuildUpdateRequest(FormValues form) => new()
    {
        Code = _model.Code.Trim().ToUpperInvariant(),
        Title = _model.Title.Trim(),
        TitleArabic = _model.TitleArabic.Trim(),
        Description = NullIfBlank(_model.Description),
        DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
        HallId = form.HallId,
        CategoryId = form.CategoryId,
        Type = form.Type,
        Start = form.Start,
        End = form.End,
        CapacityOverride = form.CapacityOverride,
        Speakers = _selectedSpeakers.ToList(),
        ThemeIds = _selectedThemes.ToList(),
        IsActive = _model.IsActive,
        LiveStreamUrl = NullIfBlank(_model.LiveStreamUrl),
        LiveSignLanguageUrl = NullIfBlank(_model.LiveSignLanguageUrl),
        // NullIfBlank is what CLEARS a notice: emptying either box sends null,
        // so a notice an admin no longer wants can actually be taken down.
        LiveNotice = NullIfBlank(_model.LiveNotice),
        LiveNoticeArabic = NullIfBlank(_model.LiveNoticeArabic),
        LiveCaptions = NullIfBlank(_model.LiveCaptions),
        LiveCaptionsArabic = NullIfBlank(_model.LiveCaptionsArabic),
        SeatSelectionModeOverride = ParseSeatModeOverride(),
        ArrivalGraceMinutesOverride = form.ArrivalGraceMinutesOverride,
        Language = NullIfBlank(_model.Language),
        LanguageArabic = NullIfBlank(_model.LanguageArabic),
        Outcomes = BuildOutcomes(),
    };


    /// <summary>D-839 — the shared 0..240 bound as the input's own `max`, so the
    /// browser refuses out-of-range values before the parser has to.</summary>
    private static string ArrivalGraceMax =>
        WalkInModeOptions.MaxArrivalGraceMinutes.ToString(CultureInfo.InvariantCulture);

    /// <summary>D-839 — the helper under the arrival-grace box. On edit it names
    /// the number the server would actually use if the box is left blank, so an
    /// admin can see what they are inheriting instead of guessing whether the hall
    /// or the global value is in force.</summary>
    private string ArrivalGraceHint() => Initial is null
        ? L["Admin.Sessions.Field.ArrivalGraceHint"]
        : string.Format(
            CultureInfo.CurrentCulture,
            L["Admin.Sessions.Field.ArrivalGraceInheritHint"],
            Initial.InheritedArrivalGraceMinutes);

    /// <summary>The form's validated, parsed values — everything the two request
    /// builders need that is not read straight off <see cref="_model"/>.</summary>
    private sealed record FormValues(
        Guid HallId,
        DateTime Start,
        DateTime End,
        int? CapacityOverride,
        Guid? CategoryId,
        SessionType? Type,
        // D-839 — null = inherit the hall.
        int? ArrivalGraceMinutesOverride);

    /// <summary>A1/A6 — true when saving would destroy seats: this is an edit, the
    /// hall or the start/end window actually moves, and something is currently held.
    /// The comparison is the same one <c>AdminSessionService.UpdateAsync</c> makes
    /// (<c>HallId</c> / <c>Start</c> / <c>End</c> against the stored row), so the
    /// warning and the cascade can never disagree.</summary>
    private bool WouldReleaseHeldSeats(Guid hallId, DateTime start, DateTime end)
    {
        if (!IsEdit || Initial is null)
        {
            return false;
        }
        var slotMoves = Initial.HallId != hallId
            || Initial.Start != start
            || Initial.End != end;
        return slotMoves && (ReleasedReservations + ReleasedAdminBlocks) > 0;
    }

    /// <summary>A1 — the admin accepted the consequence; re-run the submit, which now
    /// falls through the guard.</summary>
    private async Task ConfirmReleaseAsync()
    {
        _confirmRelease = false;
        _releaseAccepted = true;
        await HandleSubmitAsync();
    }

    private void CancelRelease() => _confirmRelease = false;

    // D-578 — subtitle import (upload .srt/.vtt parsed server-side, or fetch from the
    // video) feedback banner. Variant is one of SimfAlert's error/success/info.
    private string? _subtitleMessage;
    private string _subtitleVariant = "info";

    /// <summary>Parses an uploaded .srt/.vtt/.txt file (server-side, in-process) into a
    /// clean transcript and drops it into the matching caption field.</summary>
    private async Task OnSubtitleFileAsync(InputFileChangeEventArgs e)
    {
        _subtitleMessage = null;
        var file = e.File;
        if (file is null)
        {
            return;
        }
        // Subtitle files are tiny; cap the read at 4 MB as a safety bound.
        const long maxBytes = 4L * 1024 * 1024;
        try
        {
            using var reader = new System.IO.StreamReader(file.OpenReadStream(maxBytes));
            var text = SubtitleParser.Parse(await reader.ReadToEndAsync());
            if (string.IsNullOrWhiteSpace(text))
            {
                _subtitleVariant = "error";
                _subtitleMessage = L["Admin.Sessions.Subtitle.Empty"];
                return;
            }
            ApplyTranscript(text, IsMostlyArabic(text));
            _subtitleVariant = "success";
            _subtitleMessage = L["Admin.Sessions.Subtitle.Imported"];
        }
        catch (Exception)
        {
            _subtitleVariant = "error";
            _subtitleMessage = L["Admin.Sessions.Subtitle.ReadError"];
        }
    }

    /// <summary>Fetches the subtitle straight from the session's LiveStreamUrl video
    /// (YouTube) via the admin API. Fails gracefully where the server can't reach
    /// YouTube — the error banner then points the admin to paste/upload instead.</summary>
    private async Task FetchSubtitleAsync()
    {
        _subtitleMessage = null;
        var url = NullIfBlank(_model.LiveStreamUrl);
        if (url is null)
        {
            _subtitleVariant = "error";
            _subtitleMessage = L["Admin.Sessions.Subtitle.NoUrl"];
            return;
        }
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<FetchSubtitleResponse>>(
                "simfAccount.postJson", "/account/api/admin/sessions/subtitle/fetch-from-video",
                new FetchSubtitleRequest(url, null));
            if (envelope is { Success: true, Data: not null }
                && !string.IsNullOrWhiteSpace(envelope.Data.Text))
            {
                var isArabic = envelope.Data.LanguageCode
                    .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
                ApplyTranscript(envelope.Data.Text, isArabic);
                _subtitleVariant = "success";
                _subtitleMessage = L["Admin.Sessions.Subtitle.Fetched"];
            }
            else
            {
                _subtitleVariant = "error";
                _subtitleMessage = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Sessions.Subtitle.FetchFailed"];
            }
        }
        catch (Exception)
        {
            _subtitleVariant = "error";
            _subtitleMessage = L["Admin.Sessions.Subtitle.FetchFailed"];
        }
        finally
        {
            _busy = false;
        }
    }

    private void ApplyTranscript(string text, bool arabic)
    {
        if (arabic)
        {
            _model.LiveCaptionsArabic = text;
        }
        else
        {
            _model.LiveCaptions = text;
        }
    }

    /// <summary>True when Arabic-script characters outnumber Latin ones — routes the
    /// imported transcript to the Arabic vs English caption field.</summary>
    private static bool IsMostlyArabic(string text)
    {
        var arabic = 0;
        var latin = 0;
        foreach (var c in text)
        {
            if (c is >= '؀' and <= 'ۿ')
            {
                arabic++;
            }
            else if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))
            {
                latin++;
            }
        }
        return arabic > latin;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TitleArabic { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionArabic { get; set; } = string.Empty;
        // Website Session-detail "at a glance" language label (Figma 5991-85840).
        public string Language { get; set; } = string.Empty;
        public string LanguageArabic { get; set; } = string.Empty;
        // §8 — live broadcast stream URLs (manual stub provider).
        public string LiveStreamUrl { get; set; } = string.Empty;
        public string LiveSignLanguageUrl { get; set; } = string.Empty;
        // FR-702 — bilingual informational notice shown with the live stream.
        // Blank in both languages = no notice; it never withholds the stream.
        public string LiveNotice { get; set; } = string.Empty;
        public string LiveNoticeArabic { get; set; } = string.Empty;
        // P5 — D-439: AI live-caption text (manual stub provider, bilingual).
        public string LiveCaptions { get; set; } = string.Empty;
        public string LiveCaptionsArabic { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>§6.16 (F-U5-003) — surface a lookup failure once, into the
    /// dialog's own error area. Uses ??= so a validation message the admin is
    /// already acting on is never overwritten by a background load failure.</summary>
    private void ReportLookupFailure() => _error ??= L["Admin.Sessions.LookupsFailed"];
}
