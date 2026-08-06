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
            _model.CoverImageRelativePath = Initial.CoverImageRelativePath;
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
                _model.GalleryText = FormatGallery(env.Data.Gallery);
                _model.SessionTitlesText = FormatSessionTitles(env.Data.SessionTitles);
                _model.PastSpeakersText = FormatPastSpeakers(env.Data.PastSpeakers);
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
        CoverImageRelativePath = _model.CoverImageRelativePath,
        LocationEn = _model.LocationEn,
        LocationAr = _model.LocationAr,
        DateLabelEn = _model.DateLabelEn,
        DateLabelAr = _model.DateLabelAr,
        Gallery = ParseGallery(_model.GalleryText),
        SessionTitles = ParseSessionTitles(_model.SessionTitlesText),
        PastSpeakers = ParsePastSpeakers(_model.PastSpeakersText),
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
        CoverImageRelativePath = _model.CoverImageRelativePath,
        LocationEn = _model.LocationEn,
        LocationAr = _model.LocationAr,
        DateLabelEn = _model.DateLabelEn,
        DateLabelAr = _model.DateLabelAr,
        // Null when the detail fetch failed → keep existing rows.
        Gallery = _listsLoaded ? ParseGallery(_model.GalleryText) : null,
        SessionTitles = _listsLoaded ? ParseSessionTitles(_model.SessionTitlesText) : null,
        PastSpeakers = _listsLoaded ? ParsePastSpeakers(_model.PastSpeakersText) : null,
        IsActive = _model.IsActive,
    };

    // The save endpoint returns the detail; the host grid is keyed on the
    // summary, so project the saved row back to a summary for OnSuccess.
    private static AdminArchiveEditionSummary ToSummary(AdminArchiveEditionDetail d) =>
        new(d.Id, d.Year, d.TitleEn, d.TitleAr, d.SummaryEn, d.SummaryAr,
            d.Attendees, d.Sessions, d.Speakers, d.CoverImageRelativePath,
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
        public string? CoverImageRelativePath { get; set; }
        // §9 (screen 24-01) — place + date label.
        public string? LocationEn { get; set; }
        public string? LocationAr { get; set; }
        public string? DateLabelEn { get; set; }
        public string? DateLabelAr { get; set; }
        // The rich lists as line-delimited text (parsed on save).
        public string GalleryText { get; set; } = string.Empty;
        public string SessionTitlesText { get; set; } = string.Empty;
        public string PastSpeakersText { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    // ---- D-432 list parse / format helpers ----------------------------------

    private static IEnumerable<string[]> Rows(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.Split('|').Select(p => p.Trim()).ToArray());

    private static List<ArchiveMediaItemInput> ParseGallery(string text) =>
        Rows(text)
            .Where(p => p.Length > 0 && p[0].Length > 0)
            .Select(p => new ArchiveMediaItemInput
            {
                Url = p[0],
                Kind = p.Length > 1 && p[1].Equals("video", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                CaptionAr = p.Length > 2 && p[2].Length > 0 ? p[2] : null,
            })
            .ToList();

    private static List<ArchiveSessionTitleInput> ParseSessionTitles(string text) =>
        Rows(text)
            .Where(p => p.Length > 0 && p[0].Length > 0)
            .Select(p => new ArchiveSessionTitleInput
            {
                TitleAr = p[0],
                TitleEn = p.Length > 1 && p[1].Length > 0 ? p[1] : p[0],
            })
            .ToList();

    private static List<ArchivePastSpeakerInput> ParsePastSpeakers(string text) =>
        Rows(text)
            .Where(p => p.Length > 0 && p[0].Length > 0)
            .Select(p => new ArchivePastSpeakerInput
            {
                NameAr = p[0],
                NameEn = p.Length > 1 && p[1].Length > 0 ? p[1] : p[0],
                PhotoRelativePath = p.Length > 2 && p[2].Length > 0 ? p[2] : null,
                // 4th field: the ISO 3166-1 numeric country code (e.g. 682).
                CountryId = p.Length > 3 && int.TryParse(p[3], out var cid)
                    ? cid
                    : (int?)null,
            })
            .ToList();

    private static string FormatGallery(IReadOnlyList<ArchiveMediaItemInput>? items) =>
        items is null ? string.Empty : string.Join("\n", items.Select(i =>
            $"{i.Url} | {(i.Kind == 1 ? "video" : "image")}{(string.IsNullOrWhiteSpace(i.CaptionAr) ? string.Empty : $" | {i.CaptionAr}")}"));

    private static string FormatSessionTitles(IReadOnlyList<ArchiveSessionTitleInput>? items) =>
        items is null ? string.Empty : string.Join("\n", items.Select(i => $"{i.TitleAr} | {i.TitleEn}"));

    private static string FormatPastSpeakers(IReadOnlyList<ArchivePastSpeakerInput>? items) =>
        items is null ? string.Empty : string.Join("\n", items.Select(FormatPastSpeaker));

    // Grammar: "name-ar | name-en | photo-url | countryId". The photo
    // slot is emitted (possibly empty) whenever a country follows it, so the
    // country always lands in the 4th position on re-parse.
    private static string FormatPastSpeaker(ArchivePastSpeakerInput i)
    {
        var line = $"{i.NameAr} | {i.NameEn}";
        var hasPhoto = !string.IsNullOrWhiteSpace(i.PhotoRelativePath);
        if (hasPhoto || i.CountryId.HasValue)
        {
            line += $" | {(hasPhoto ? i.PhotoRelativePath : string.Empty)}";
        }
        if (i.CountryId.HasValue)
        {
            line += $" | {i.CountryId.Value}";
        }
        return line;
    }
}
