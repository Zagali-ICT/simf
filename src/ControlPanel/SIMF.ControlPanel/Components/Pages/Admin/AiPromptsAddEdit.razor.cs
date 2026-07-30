using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Ai;
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiPromptsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FormModel _model = new();
    // Feature + Provider are SimfSelect<string>-bound: held as int-string ids and
    // parsed back to the enums at submit (mirrors SessionsAddEdit's enum selects).
    private string _featureInput = AiEnumOptions.Id(AiFeature.QuestionFilter);
    private string _providerInput = AiEnumOptions.Id(AiProvider.Echo);
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Key = Initial.Key;
            _featureInput = AiEnumOptions.Id(Initial.Feature);
            _model.DisplayName = Initial.DisplayName;
            _model.DisplayNameArabic = Initial.DisplayNameArabic;
            _model.Description = Initial.Description;
            _model.DescriptionArabic = Initial.DescriptionArabic;
            _providerInput = AiEnumOptions.Id(Initial.Provider);
            _model.Model = Initial.Model;
            _model.SystemPrompt = Initial.SystemPrompt;
            _model.UserPromptTemplate = Initial.UserPromptTemplate;
            _model.Temperature = Initial.Temperature;
            _model.MaxOutputTokens = Initial.MaxOutputTokens;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (!IsEdit && string.IsNullOrWhiteSpace(_model.Key))
        {
            _error = L["Admin.AiPrompts.Field.KeyRequired"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.DisplayName)
            || string.IsNullOrWhiteSpace(_model.DisplayNameArabic))
        {
            _error = L["Admin.AiPrompts.Required"]; return;
        }

        _busy = true;
        try
        {
            var result = await SendAsync(
                JS,
                "/account/api/admin/ai/prompts",
                $"/account/api/admin/ai/prompts/{Initial?.Id}",
                new CreateAiPromptRequest
                {
                    Key = _model.Key.Trim(),
                    Feature = AiEnumOptions.ParseFeature(_featureInput),
                    DisplayName = _model.DisplayName.Trim(),
                    DisplayNameArabic = _model.DisplayNameArabic.Trim(),
                    Description = _model.Description,
                    DescriptionArabic = _model.DescriptionArabic,
                    Provider = AiEnumOptions.ParseProvider(_providerInput),
                    Model = _model.Model,
                    SystemPrompt = _model.SystemPrompt,
                    UserPromptTemplate = _model.UserPromptTemplate,
                    Temperature = _model.Temperature,
                    MaxOutputTokens = _model.MaxOutputTokens,
                },
                new UpdateAiPromptRequest
                {
                    Feature = AiEnumOptions.ParseFeature(_featureInput),
                    DisplayName = _model.DisplayName.Trim(),
                    DisplayNameArabic = _model.DisplayNameArabic.Trim(),
                    Description = _model.Description,
                    DescriptionArabic = _model.DescriptionArabic,
                    Provider = AiEnumOptions.ParseProvider(_providerInput),
                    Model = _model.Model,
                    SystemPrompt = _model.SystemPrompt,
                    UserPromptTemplate = _model.UserPromptTemplate,
                    Temperature = _model.Temperature,
                    MaxOutputTokens = _model.MaxOutputTokens,
                    IsActive = _model.IsActive,
                });

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.AiPrompts.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    // Feature/Provider option lists, labels and parse live in the shared
    // AiEnumOptions helper (also used by the services-console routing editor).

    private void OnSystemPromptChanged(ChangeEventArgs e) =>
        _model.SystemPrompt = e.Value?.ToString() ?? string.Empty;

    private void OnUserPromptChanged(ChangeEventArgs e) =>
        _model.UserPromptTemplate = e.Value?.ToString() ?? string.Empty;

    private void OnTemperatureChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            _model.Temperature = d;
    }

    private void OnMaxTokensChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n))
            _model.MaxOutputTokens = n;
    }

    // The form's view-model. Named FormModel (not Model) because it carries a
    // Model property (the AI model id) — a property cannot share its enclosing
    // type's name (CS0542).
    private sealed class FormModel
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DisplayNameArabic { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionArabic { get; set; }
        public string Model { get; set; } = "echo";
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPromptTemplate { get; set; } = string.Empty;
        public double Temperature { get; set; } = 0.2;
        public int MaxOutputTokens { get; set; } = 512;
        public bool IsActive { get; set; } = true;
    }
}
