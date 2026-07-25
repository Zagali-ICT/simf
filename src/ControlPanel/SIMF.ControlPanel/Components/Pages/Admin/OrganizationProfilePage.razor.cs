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
using SIMF.Contracts.Organization;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OrganizationProfilePage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _loading = true;
    private bool _busy;
    private string? _toastMessage;
    private string _toastVariant = "success";
    private bool _videoBusy;

    // D-768 — the hidden file input the hero-video upload reads, and the served
    // route the upload points BackgroundVideoUrl at. HasUploadedHeroVideo shows the
    // "Remove" affordance only when an uploaded video (not a pasted external /
    // YouTube link) is the current hero source.
    private const string HeroVideoInputId = "org-hero-video-input";
    // Keep in sync with OrganizationHeroVideoRoutes.StreamRoute (the API assembly the
    // CP does not reference). If the served route changes, the "Remove" affordance
    // silently stops appearing until this matches.
    private const string HeroVideoRouteSuffix = "/app/organization/hero-video.mp4";
    private bool HasUploadedHeroVideo =>
        (_model.BackgroundVideoUrl ?? string.Empty)
            .EndsWith(HeroVideoRouteSuffix, StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        _editContext = new EditContext(_model);
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<OrganizationProfileResponse>>(
                "simfAccount.getJson", "/account/api/admin/organization-profile");
            if (envelope is { Success: true, Data: not null })
            {
                Load(envelope.Data);
            }
            else
            {
                _toastVariant = "error";
                _toastMessage = L["Admin.OrganizationProfile.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.OrganizationProfile.LoadFailed"];
        }
        finally { _loading = false; }
    }

    private void Load(OrganizationProfileResponse d)
    {
        _model.Name = d.Name;
        _model.NameArabic = d.NameArabic;
        _model.Title = d.Title;
        _model.TitleArabic = d.TitleArabic;
        _model.Slogan = d.Slogan ?? string.Empty;
        _model.SloganArabic = d.SloganArabic ?? string.Empty;
        _model.Bio = d.Bio ?? string.Empty;
        _model.BioArabic = d.BioArabic ?? string.Empty;
        _model.CurrentYear = d.CurrentYear.ToString(CultureInfo.InvariantCulture);
        _model.Status = d.Status;
        _model.EventStartDate = DateString(d.EventStartDate);
        _model.EventEndDate = DateString(d.EventEndDate);
        _model.Version = d.Version ?? string.Empty;
        _model.SysVersion = d.SysVersion ?? string.Empty;
        _model.LocationText = d.LocationText ?? string.Empty;
        _model.LocationTextArabic = d.LocationTextArabic ?? string.Empty;
        _model.Latitude = d.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _model.Longitude = d.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _model.ContactPhone = d.ContactPhone ?? string.Empty;
        _model.ContactEmail = d.ContactEmail ?? string.Empty;
        _model.ContactWebsite = d.ContactWebsite ?? string.Empty;
        _model.LiveStreamUrl = d.LiveStreamUrl ?? string.Empty;
        _model.BackgroundVideoUrl = d.BackgroundVideoUrl ?? string.Empty;
        _model.Facebook = d.Social.Facebook ?? string.Empty;
        _model.X = d.Social.X ?? string.Empty;
        _model.Instagram = d.Social.Instagram ?? string.Empty;
        _model.LinkedIn = d.Social.LinkedIn ?? string.Empty;
        _model.YouTube = d.Social.YouTube ?? string.Empty;
        _model.TikTok = d.Social.TikTok ?? string.Empty;
        _model.Snapchat = d.Social.Snapchat ?? string.Empty;
        _model.AboutItems = d.AboutItems
            .Select(a => new AboutRow
            {
                Id = a.Id, Title = a.Title, TitleArabic = a.TitleArabic,
                Text = a.Text, TextArabic = a.TextArabic,
            }).ToList();
        _model.Details = d.Details
            .Select(x => new DetailRow
            {
                Id = x.Id, Name = x.Name, NameArabic = x.NameArabic,
                Value = x.Value, ValueArabic = x.ValueArabic ?? string.Empty,
            }).ToList();
    }

    private void AddAbout() => _model.AboutItems.Add(new AboutRow());
    private void RemoveAbout(AboutRow row) => _model.AboutItems.Remove(row);
    private void MoveAbout(AboutRow row, int delta) => Move(_model.AboutItems, row, delta);

    private void AddDetail() => _model.Details.Add(new DetailRow());
    private void RemoveDetail(DetailRow row) => _model.Details.Remove(row);
    private void MoveDetail(DetailRow row, int delta) => Move(_model.Details, row, delta);

    // Swap a row with its neighbour so the admin can reorder the list; the array
    // index becomes the persisted DisplayOrder on save.
    private static void Move<T>(List<T> list, T row, int delta)
    {
        var i = list.IndexOf(row);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) { return; }
        (list[i], list[j]) = (list[j], list[i]);
    }

    private async Task SaveAsync()
    {
        if (_busy) { return; }
        _busy = true;
        _toastMessage = null;
        try
        {
            var request = new AdminUpdateOrganizationProfileRequest
            {
                Name = _model.Name,
                NameArabic = _model.NameArabic,
                Title = _model.Title,
                TitleArabic = _model.TitleArabic,
                Slogan = _model.Slogan,
                SloganArabic = _model.SloganArabic,
                Bio = _model.Bio,
                BioArabic = _model.BioArabic,
                CurrentYear = int.TryParse(_model.CurrentYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ? y : 0,
                Status = _model.Status,
                EventStartDate = ParseDate(_model.EventStartDate),
                EventEndDate = ParseDate(_model.EventEndDate),
                Version = _model.Version,
                SysVersion = _model.SysVersion,
                LocationText = _model.LocationText,
                LocationTextArabic = _model.LocationTextArabic,
                Latitude = ParseDecimal(_model.Latitude),
                Longitude = ParseDecimal(_model.Longitude),
                ContactPhone = _model.ContactPhone,
                ContactEmail = _model.ContactEmail,
                ContactWebsite = _model.ContactWebsite,
                LiveStreamUrl = _model.LiveStreamUrl,
                BackgroundVideoUrl = _model.BackgroundVideoUrl,
                Facebook = _model.Facebook,
                X = _model.X,
                Instagram = _model.Instagram,
                LinkedIn = _model.LinkedIn,
                YouTube = _model.YouTube,
                TikTok = _model.TikTok,
                Snapchat = _model.Snapchat,
                AboutItems = _model.AboutItems
                    .Select((a, i) => new AdminOrganizationAboutItem
                    {
                        Id = a.Id, Title = a.Title, TitleArabic = a.TitleArabic,
                        Text = a.Text, TextArabic = a.TextArabic, DisplayOrder = i,
                    }).ToList(),
                Details = _model.Details
                    .Select((x, i) => new AdminOrganizationDetail
                    {
                        Id = x.Id, Name = x.Name, NameArabic = x.NameArabic,
                        Value = x.Value, ValueArabic = x.ValueArabic, DisplayOrder = i,
                    }).ToList(),
            };

            var envelope = await JS.InvokeAsync<ApiResult<OrganizationProfileResponse>>(
                "simfAccount.putJson", "/account/api/admin/organization-profile", request);
            if (envelope is { Success: true, Data: not null })
            {
                Load(envelope.Data);
                _toastVariant = "success";
                _toastMessage = L["Admin.OrganizationProfile.Saved"];
            }
            else
            {
                _toastVariant = "error";
                _toastMessage = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.OrganizationProfile.SaveFailed"];
            }
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.OrganizationProfile.SaveFailed"];
        }
        finally { _busy = false; }
    }

    // D-768 — upload the picked hero video through the CP proxy (streamed to the
    // API), then reload so the served BackgroundVideoUrl + the Remove affordance
    // reflect the new state.
    private async Task UploadHeroVideoAsync()
    {
        if (_busy || _videoBusy) { return; }
        _videoBusy = true;
        _toastMessage = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<OrganizationProfileResponse>>(
                "simfAccount.uploadFile",
                "/account/api/admin/organization-profile/hero-video", HeroVideoInputId);
            ApplyHeroVideoResult(envelope, L["Admin.OrganizationProfile.HeroVideoUploaded"]);
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.OrganizationProfile.HeroVideoFailed"];
        }
        finally { _videoBusy = false; }
    }

    private async Task RemoveHeroVideoAsync()
    {
        if (_busy || _videoBusy) { return; }
        _videoBusy = true;
        _toastMessage = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<OrganizationProfileResponse>>(
                "simfAccount.deleteJson", "/account/api/admin/organization-profile/hero-video");
            ApplyHeroVideoResult(envelope, L["Admin.OrganizationProfile.HeroVideoRemoved"]);
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.OrganizationProfile.HeroVideoFailed"];
        }
        finally { _videoBusy = false; }
    }

    // Reload the form from the returned profile on success (so BackgroundVideoUrl +
    // HasUploadedHeroVideo update), else surface the server error.
    private void ApplyHeroVideoResult(ApiResult<OrganizationProfileResponse>? envelope, string successMessage)
    {
        if (envelope is { Success: true, Data: not null })
        {
            Load(envelope.Data);
            _toastVariant = "success";
            _toastMessage = successMessage;
        }
        else
        {
            _toastVariant = "error";
            _toastMessage = envelope?.Error?.MessageForCurrentCulture()
                ?? L["Admin.OrganizationProfile.HeroVideoFailed"];
        }
    }

    private static string DateString(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd") ?? string.Empty;

    private static DateTimeOffset? ParseDate(string? raw) =>
        DateTimeOffset.TryParse(raw, out var d) ? d : null;

    private static decimal? ParseDecimal(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TitleArabic { get; set; } = string.Empty;
        public string Slogan { get; set; } = string.Empty;
        public string SloganArabic { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string BioArabic { get; set; } = string.Empty;
        public string CurrentYear { get; set; } = "2026";
        public string Status { get; set; } = "Open";
        public string EventStartDate { get; set; } = string.Empty;
        public string EventEndDate { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string SysVersion { get; set; } = string.Empty;
        public string LocationText { get; set; } = string.Empty;
        public string LocationTextArabic { get; set; } = string.Empty;
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactWebsite { get; set; } = string.Empty;
        public string LiveStreamUrl { get; set; } = string.Empty;
        public string BackgroundVideoUrl { get; set; } = string.Empty;
        public string Facebook { get; set; } = string.Empty;
        public string X { get; set; } = string.Empty;
        public string Instagram { get; set; } = string.Empty;
        public string LinkedIn { get; set; } = string.Empty;
        public string YouTube { get; set; } = string.Empty;
        public string TikTok { get; set; } = string.Empty;
        public string Snapchat { get; set; } = string.Empty;
        public List<AboutRow> AboutItems { get; set; } = new();
        public List<DetailRow> Details { get; set; } = new();
    }

    private sealed class AboutRow
    {
        public Guid? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleArabic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string TextArabic { get; set; } = string.Empty;
    }

    private sealed class DetailRow
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ValueArabic { get; set; } = string.Empty;
    }
}
