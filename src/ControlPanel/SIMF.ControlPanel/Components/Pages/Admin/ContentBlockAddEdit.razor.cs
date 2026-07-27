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

public partial class ContentBlockAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Key = Initial.Key;
            _model.Content = Initial.Content;
            _model.ContentArabic = Initial.ContentArabic;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Key) || _model.Key.Trim().Length > 128)
        {
            _error = L["Admin.ContentBlocks.Required"]; return;
        }

        _busy = true;
        try
        {
            // Upsert-by-Key — one PUT serves both Add and Edit (the row is
            // created if the Key is new, updated in place if it exists).
            var envelope = await JS.InvokeAsync<ApiResult<AdminContentBlockSummary>>(
                "simfAccount.putJson", "/account/api/admin/content-blocks",
                new UpsertContentBlockRequest
                {
                    Key = _model.Key.Trim(),
                    Content = _model.Content,
                    ContentArabic = _model.ContentArabic,
                    IsActive = _model.IsActive,
                });

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ContentBlocks.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.ContentBlocks.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Key { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ContentArabic { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
