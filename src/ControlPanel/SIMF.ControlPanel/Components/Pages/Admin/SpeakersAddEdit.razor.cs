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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SpeakersAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _displayOrderInput = "0";
    private string _countryIdInput = string.Empty;
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // Country picker state — populated lazily on first render.
    private string[] _countryIds = Array.Empty<string>();
    private Dictionary<string, AdminCountrySummary> _countriesById = new();

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.Rank = Initial.Rank ?? string.Empty;
            _model.RankArabic = Initial.RankArabic ?? string.Empty;
            _countryIdInput = Initial.CountryId?.ToString() ?? string.Empty;
            _model.Bio = Initial.Bio ?? string.Empty;
            _model.BioArabic = Initial.BioArabic ?? string.Empty;
            _model.Qualifications = Initial.Qualifications ?? string.Empty;
            _model.QualificationsArabic = Initial.QualificationsArabic ?? string.Empty;
            _model.TrainingExperience = Initial.TrainingExperience ?? string.Empty;
            _model.TrainingExperienceArabic = Initial.TrainingExperienceArabic ?? string.Empty;
            _model.Awards = Initial.Awards ?? string.Empty;
            _model.AwardsArabic = Initial.AwardsArabic ?? string.Empty;
            _model.AllowsMeetingRequests = Initial.AllowsMeetingRequests;
            _model.AllowsDataSharing = Initial.AllowsDataSharing;
            _model.FacebookUrl = Initial.FacebookUrl ?? string.Empty;
            _model.LinkedInUrl = Initial.LinkedInUrl ?? string.Empty;
            _model.XUrl = Initial.XUrl ?? string.Empty;
            _model.WebsiteUrl = Initial.WebsiteUrl ?? string.Empty;
            _model.IsActive = Initial.IsActive;
            _displayOrderInput = Initial.DisplayOrder.ToString();
        }
        _editContext = new EditContext(_model);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminCountrySummary>>>(
                "simfAccount.postJson", "/account/api/admin/countries/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (envelope is { Success: true, Data: not null })
            {
                _countriesById = envelope.Data.Items.ToDictionary(c => c.Id.ToString(), c => c);
                _countryIds = envelope.Data.Items
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => c.Id.ToString())
                    .ToArray();
                StateHasChanged();
            }
        }
        catch
        {
            // Picker stays empty; admin can still submit with no country.
        }
    }

    private string CountryLabel(string id)
    {
        if (!_countriesById.TryGetValue(id, out var country)) return id;
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        return isArabic
            ? $"{country.NameArabic} ({country.Code})"
            : $"{country.Name} ({country.Code})";
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Code) || _model.Code.Length is < 2 or > 16)
        {
            _error = L["Admin.Speakers.Field.CodeInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        {
            _error = L["Admin.Speakers.Field.NameInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        {
            _error = L["Admin.Speakers.Field.NameArabicInvalid"]; return;
        }
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            _error = L["Admin.Speakers.Field.DisplayOrderInvalid"]; return;
        }

        int? countryId = null;
        if (!string.IsNullOrWhiteSpace(_countryIdInput))
        {
            if (!int.TryParse(_countryIdInput, out var parsed) || parsed <= 0)
            {
                _error = L["Admin.Speakers.Field.CountryInvalid"]; return;
            }
            countryId = parsed;
        }

        _busy = true;
        try
        {
            ApiResult<AdminSpeakerDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminSpeakerDetail>>(
                    "simfAccount.postJson", "/account/api/admin/speakers",
                    new AdminCreateSpeakerRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        Rank = NullIfBlank(_model.Rank),
                        RankArabic = NullIfBlank(_model.RankArabic),
                        CountryId = countryId,
                        Bio = NullIfBlank(_model.Bio),
                        BioArabic = NullIfBlank(_model.BioArabic),
                        Qualifications = NullIfBlank(_model.Qualifications),
                        QualificationsArabic = NullIfBlank(_model.QualificationsArabic),
                        TrainingExperience = NullIfBlank(_model.TrainingExperience),
                        TrainingExperienceArabic = NullIfBlank(_model.TrainingExperienceArabic),
                        Awards = NullIfBlank(_model.Awards),
                        AwardsArabic = NullIfBlank(_model.AwardsArabic),
                        AllowsMeetingRequests = _model.AllowsMeetingRequests,
                        AllowsDataSharing = _model.AllowsDataSharing,
                        FacebookUrl = NullIfBlank(_model.FacebookUrl),
                        LinkedInUrl = NullIfBlank(_model.LinkedInUrl),
                        XUrl = NullIfBlank(_model.XUrl),
                        WebsiteUrl = NullIfBlank(_model.WebsiteUrl),
                        DisplayOrder = order,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminSpeakerDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/speakers/{Initial!.Id}",
                    new AdminUpdateSpeakerRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        Rank = NullIfBlank(_model.Rank),
                        RankArabic = NullIfBlank(_model.RankArabic),
                        CountryId = countryId,
                        UserProfileId = Initial.UserProfileId, // preserved (not editable in this form yet)
                        Bio = NullIfBlank(_model.Bio),
                        BioArabic = NullIfBlank(_model.BioArabic),
                        Qualifications = NullIfBlank(_model.Qualifications),
                        QualificationsArabic = NullIfBlank(_model.QualificationsArabic),
                        TrainingExperience = NullIfBlank(_model.TrainingExperience),
                        TrainingExperienceArabic = NullIfBlank(_model.TrainingExperienceArabic),
                        Awards = NullIfBlank(_model.Awards),
                        AwardsArabic = NullIfBlank(_model.AwardsArabic),
                        AllowsMeetingRequests = _model.AllowsMeetingRequests,
                        AllowsDataSharing = _model.AllowsDataSharing,
                        FacebookUrl = NullIfBlank(_model.FacebookUrl),
                        LinkedInUrl = NullIfBlank(_model.LinkedInUrl),
                        XUrl = NullIfBlank(_model.XUrl),
                        WebsiteUrl = NullIfBlank(_model.WebsiteUrl),
                        DisplayOrder = order,
                        IsActive = _model.IsActive,
                    });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Speakers.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Speakers.Fallback"];
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
        public string RankArabic { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string BioArabic { get; set; } = string.Empty;
        public string Qualifications { get; set; } = string.Empty;
        public string QualificationsArabic { get; set; } = string.Empty;
        public string TrainingExperience { get; set; } = string.Empty;
        public string TrainingExperienceArabic { get; set; } = string.Empty;
        public string Awards { get; set; } = string.Empty;
        public string AwardsArabic { get; set; } = string.Empty;
        public bool AllowsMeetingRequests { get; set; }
        public bool AllowsDataSharing { get; set; }
        public string FacebookUrl { get; set; } = string.Empty;
        public string LinkedInUrl { get; set; } = string.Empty;
        public string XUrl { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
