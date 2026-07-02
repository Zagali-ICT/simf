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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiRoutingEditor
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The prompt to retarget (the service's active prompt).</summary>
    [Parameter, EditorRequired] public Guid PromptId { get; set; }

    /// <summary>Raised after a successful save.</summary>
    [Parameter] public EventCallback OnSaved { get; set; }

    /// <summary>Optional cancel (renders a Cancel button when bound).</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private AdminAiPromptDetail? _detail;
    private string _provider = string.Empty;
    private string _model = string.Empty;
    private double _temp;
    private int _max;
    private bool _busy;
    private string? _error;

    protected override async Task OnParametersSetAsync()
    {
        // Reload only when the target prompt changes (re-renders must not refetch).
        if (_detail is not null && _detail.Id == PromptId)
        {
            return;
        }
        _detail = null;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _error = null;
        var env = await JS.InvokeAsync<ApiResult<AdminAiPromptDetail>>(
            "simfAccount.getJson", $"/account/api/admin/ai/prompts/{PromptId}");
        if (env is { Success: true, Data: not null })
        {
            _detail = env.Data;
            _provider = AiEnumOptions.Id(_detail.Provider);
            _model = _detail.Model;
            _temp = _detail.Temperature;
            _max = _detail.MaxOutputTokens;
        }
        else
        {
            _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.AiServices.LoadFailed"];
        }
    }

    private void OnTempChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var d)) _temp = d;
    }

    private void OnMaxChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _max = n;
    }

    private async Task SaveAsync()
    {
        if (_detail is null || _busy) return;
        _busy = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminAiPromptDetail>>(
                "simfAccount.putJson", $"/account/api/admin/ai/prompts/{_detail.Id}",
                new UpdateAiPromptRequest
                {
                    Feature = _detail.Feature,
                    DisplayName = _detail.DisplayName,
                    DisplayNameArabic = _detail.DisplayNameArabic,
                    Description = _detail.Description,
                    DescriptionArabic = _detail.DescriptionArabic,
                    Provider = AiEnumOptions.ParseProvider(_provider),
                    Model = _model,
                    SystemPrompt = _detail.SystemPrompt,
                    UserPromptTemplate = _detail.UserPromptTemplate,
                    Temperature = _temp,
                    MaxOutputTokens = _max,
                    IsActive = _detail.IsActive,
                });
            if (env is { Success: true })
            {
                await OnSaved.InvokeAsync();
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiServices.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }
}
