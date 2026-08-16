using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Archive;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ArchiveAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;
    // True once the rich lists are safe to send (create, or an edit
    // whose detail fetch succeeded). False ⇒ send null lists (keep existing).
    private bool _listsLoaded;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Year = Initial.Year;
            _model.TitleEn = Initial.TitleEn;
            _model.TitleAr = Initial.TitleAr;
            _model.SummaryEn = Initial.SummaryEn;
            _model.SummaryAr = Initial.SummaryAr;
            _model.Attendees = Initial.Attendees;
            _model.Sessions = Initial.Sessions;
            _model.Speakers = Initial.Speakers;
            _model.LocationEn = Initial.LocationEn;
            _model.LocationAr = Initial.LocationAr;
            _model.DateLabelEn = Initial.DateLabelEn;
            _model.DateLabelAr = Initial.DateLabelAr;
            _model.IsActive = Initial.IsActive;
            // The rich lists (gallery / session titles / past speakers) load
            // asynchronously in OnInitializedAsync; _listsLoaded stays false until
            // that detail fetch succeeds, so an edit submitted before it lands
            // sends null (keep existing) for those lists.
        }
        else
        {
            _model.Year = SimfClock.Now.Year;
            _listsLoaded = true; // create authors the lists from scratch
        }

        // Build the EditContext synchronously, before any await, so the form's
        // intermediate render (while the edit-mode detail fetch below is still in
        // flight) always has a non-null EditContext for <EditForm>. A null
        // EditContext there throws and terminates the circuit. Mirrors every
        // sibling Add/Edit form (Speakers, Sessions, Banners, ...).
        _editContext = new EditContext(_model);
    }

    protected override async Task OnInitializedAsync()
    {
        if (Initial is null)
        {
            return;
        }

        // The grid summary carries no rich lists; fetch the detail to
        // pre-populate the gallery / session-title / past-speaker textareas.
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminArchiveEditionDetail>>(
                "simfAccount.getJson", $"/account/api/admin/archive/{Initial.Id}");
            if (env is { Success: true, Data: not null })
            {
                _model.Gallery = [.. env.Data.Gallery ?? []];
                _model.SessionTitlesText = FormatSessionTitles(env.Data.SessionTitles);
                _model.PastSpeakers = [.. env.Data.PastSpeakers ?? []];
                _listsLoaded = true;
            }
        }
        catch (Exception)
        {
            // Leave the textareas empty on a fetch failure; an edit that does
            // not touch them sends null (keep) so nothing is lost.
        }
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.TitleEn)
            || string.IsNullOrWhiteSpace(_model.TitleAr))
        {
            _error = L["Admin.Archive.Validation.TitleRequired"]; return;
        }

        _busy = true;
        try
        {
            // SendAsync on the base is not usable here: OnSuccess carries the
            // grid SUMMARY while the API answers with the DETAIL, so the result
            // has to be mapped through ToSummary before it is raised.
            var envelope = IsEdit
                ? await JS.InvokeAsync<ApiResult<AdminArchiveEditionDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/archive/{Initial!.Id}",
                    BuildUpdateRequest())
                : await JS.InvokeAsync<ApiResult<AdminArchiveEditionDetail>>(
                    "simfAccount.postJson", "/account/api/admin/archive",
                    BuildCreateRequest());

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(ToSummary(envelope.Data));
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Archive.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Archive.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private CreateArchiveEditionRequest BuildCreateRequest() => new()
    {
        Year = _model.Year,
        TitleEn = _model.TitleEn.Trim(),
        TitleAr = _model.TitleAr.Trim(),
        SummaryEn = _model.SummaryEn,
        SummaryAr = _model.SummaryAr,
        Attendees = _model.Attendees,
        Sessions = _model.Sessions,
        Speakers = _model.Speakers,
        LocationEn = _model.LocationEn,
        LocationAr = _model.LocationAr,
        DateLabelEn = _model.DateLabelEn,
        DateLabelAr = _model.DateLabelAr,
        Gallery = _model.Gallery,
        SessionTitles = ParseSessionTitles(_model.SessionTitlesText),
        PastSpeakers = _model.PastSpeakers,
    };

    private UpdateArchiveEditionRequest BuildUpdateRequest() => new()
    {
        Year = _model.Year,
        TitleEn = _model.TitleEn.Trim(),
        TitleAr = _model.TitleAr.Trim(),
        SummaryEn = _model.SummaryEn,
        SummaryAr = _model.SummaryAr,
        Attendees = _model.Attendees,
        Sessions = _model.Sessions,
        Speakers = _model.Speakers,
        LocationEn = _model.LocationEn,
        LocationAr = _model.LocationAr,
        DateLabelEn = _model.DateLabelEn,
        DateLabelAr = _model.DateLabelAr,
        // Null when the detail fetch failed → keep existing rows.
        Gallery = _listsLoaded ? _model.Gallery : null,
        SessionTitles = _listsLoaded ? ParseSessionTitles(_model.SessionTitlesText) : null,
        PastSpeakers = _listsLoaded ? _model.PastSpeakers : null,
        IsActive = _model.IsActive,
    };

    // The save endpoint returns the detail; the host grid is keyed on the
    // summary, so project the saved row back to a summary for OnSuccess.
    private static AdminArchiveEditionSummary ToSummary(AdminArchiveEditionDetail d) =>
        new(d.Id, d.Year, d.TitleEn, d.TitleAr, d.SummaryEn, d.SummaryAr,
            d.Attendees, d.Sessions, d.Speakers,
            // The cover is a StoredFile; the wire field survives carrying null.
            null,
            d.IsActive, d.CreatedAt,
            // HasCover — optimistic false; OnSavedAsync reloads the grid, which
            // recomputes it from the ArchiveCover asset presence.
            false,
            d.LocationEn, d.LocationAr, d.DateLabelEn, d.DateLabelAr);

    private void OnYearChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.Year = n;
    }

    private void OnAttendeesChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.Attendees = n;
    }

    private void OnSessionsChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.Sessions = n;
    }

    private void OnSpeakersChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.Speakers = n;
    }

    private void OnSummaryEnChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _model.SummaryEn = string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private void OnSummaryArChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _model.SummaryAr = string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private sealed class Model
    {
        public int Year { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? SummaryEn { get; set; }
        public string? SummaryAr { get; set; }
        public int Attendees { get; set; }
        public int Sessions { get; set; }
        public int Speakers { get; set; }
        // Place + date label.
        public string? LocationEn { get; set; }
        public string? LocationAr { get; set; }
        public string? DateLabelEn { get; set; }
        public string? DateLabelAr { get; set; }
        // Gallery and past speakers are per-row now, because each row owns an
        // uploaded file and a file needs a row id to belong to. Session titles
        // carry no media, so they stay line-delimited text.
        public List<ArchiveMediaItemInput> Gallery { get; set; } = [];
        public List<ArchivePastSpeakerInput> PastSpeakers { get; set; } = [];
        public string SessionTitlesText { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    // ---- list parse / format helpers ----------------------------------------

    private static IEnumerable<string[]> Rows(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.Split('|').Select(p => p.Trim()).ToArray());

    private static List<ArchiveSessionTitleInput> ParseSessionTitles(string text) =>
        Rows(text)
            .Where(p => p.Length > 0 && p[0].Length > 0)
            .Select(p => new ArchiveSessionTitleInput
            {
                TitleAr = p[0],
                TitleEn = p.Length > 1 && p[1].Length > 0 ? p[1] : p[0],
            })
            .ToList();

    private static string FormatSessionTitles(IReadOnlyList<ArchiveSessionTitleInput>? items) =>
        items is null ? string.Empty : string.Join("\n", items.Select(i => $"{i.TitleAr} | {i.TitleEn}"));

    // ---- repeater row commands ---------------------------------------------
    //
    // A new row has a null Id until the edition is saved; the server assigns one
    // and returns it, and from then on that row keeps its identity across saves.
    // That is what lets the row own an uploaded photo: before the lists were
    // reconciled by id, every save replaced each child with a brand-new record
    // and any file attached to the old one was orphaned on the spot.

    private void AddGalleryRow() => _model.Gallery.Add(new ArchiveMediaItemInput());

    private void RemoveGalleryRow(ArchiveMediaItemInput row) => _model.Gallery.Remove(row);

    private void MoveGalleryRow(ArchiveMediaItemInput row, int delta) =>
        Move(_model.Gallery, row, delta);

    private void AddPastSpeakerRow() => _model.PastSpeakers.Add(new ArchivePastSpeakerInput());

    private void RemovePastSpeakerRow(ArchivePastSpeakerInput row) =>
        _model.PastSpeakers.Remove(row);

    private void MovePastSpeakerRow(ArchivePastSpeakerInput row, int delta) =>
        Move(_model.PastSpeakers, row, delta);

    private static void Move<T>(List<T> rows, T row, int delta)
    {
        var from = rows.IndexOf(row);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= rows.Count)
        {
            return;
        }
        rows.RemoveAt(from);
        rows.Insert(to, row);
    }
}
