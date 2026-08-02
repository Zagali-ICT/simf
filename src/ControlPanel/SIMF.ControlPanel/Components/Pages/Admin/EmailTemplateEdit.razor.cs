using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Email;

namespace SIMF.ControlPanel.Components.Pages.Admin;

// D-735 — the email-template editor. Reuses the AiPrompts house transport
// (the JS proxy → SimfAdminClient → backend) and the CrudFormBase parameter
// surface, adding the three affordances that make the feature: click-to-insert
// token chips, a debounced live preview, and reset-to-default.
public partial class EmailTemplateEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // The two body textareas carry stable ids so the caret-insert JS helper can
    // find whichever one the admin last focused.
    private const string BodyEnId = "email-template-body-en";
    private const string BodyArId = "email-template-body-ar";

    /// <summary>Raised after a successful reset-to-default. The editor stays open
    /// showing the (now built-in) defaults; the host reloads the grid so the
    /// "Customised" column reflects the change.</summary>
    [Parameter] public EventCallback<AdminEmailTemplateDetail> OnReset { get; set; }

    // Server truth for the selected type — refreshed on reset. Type / TypeName /
    // Version / IsOverride / Tokens come from here; the editable copy below.
    private AdminEmailTemplateDetail? _detail;

    private string _subject = string.Empty;
    private string _bodyEn = string.Empty;
    private string _bodyAr = string.Empty;
    private bool _isActive = true;

    private string _lastFocused = BodyEnId;

    private EmailTemplatePreviewResult? _preview;
    private IReadOnlyList<string> _unknownTokens = Array.Empty<string>();

    private bool _busy;          // a save / reset mutation is in flight
    private bool _previewing;    // a preview render is in flight
    private bool _confirmingReset;
    private string? _error;

    private bool Working => _busy || _previewing;

    // Block save while a mutation/preview is running or the copy references a
    // token this template type does not define.
    private bool CanSave => !Working && _unknownTokens.Count == 0;

    protected override async Task OnInitializedAsync()
    {
        if (Initial is not null)
        {
            LoadFromDetail(Initial);
            await PreviewAsync();
        }
    }

    private void LoadFromDetail(AdminEmailTemplateDetail detail)
    {
        _detail = detail;
        _subject = detail.Subject;
        _bodyEn = detail.BodyEn;
        _bodyAr = detail.BodyAr;
        _isActive = detail.IsActive;
    }

    // The route segment is the enum NAME (case-insensitive at the backend) — not
    // the DTO's friendly TypeName, which is display text.
    private string RouteType => _detail?.Type.ToString() ?? string.Empty;

    private static string TokenTitle(EmailTemplateTokenDto token) =>
        $"{{{token.Name}}} · {token.Sample}";

    private static string TokenLabel(EmailTemplateTokenDto token) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? token.DisplayAr
            : token.DisplayEn;

    private async Task OnSubjectChanged(ChangeEventArgs e)
    {
        _subject = e.Value?.ToString() ?? string.Empty;
        await PreviewAsync();
    }

    private async Task OnBodyEnChanged(ChangeEventArgs e)
    {
        _bodyEn = e.Value?.ToString() ?? string.Empty;
        await PreviewAsync();
    }

    private async Task OnBodyArChanged(ChangeEventArgs e)
    {
        _bodyAr = e.Value?.ToString() ?? string.Empty;
        await PreviewAsync();
    }

    // Insert {Name} at the caret of the last-focused body via the shared JS
    // helper, sync the model back from the returned value, then re-preview.
    private async Task InsertTokenAsync(string name)
    {
        if (Working) return;
        var token = "{" + name + "}";
        var next = await JS.InvokeAsync<string>(
            "simfAccount.insertAtCaret", _lastFocused, token);
        if (string.Equals(_lastFocused, BodyArId, StringComparison.Ordinal))
        {
            _bodyAr = next;
        }
        else
        {
            _bodyEn = next;
        }
        await PreviewAsync();
    }

    private async Task PreviewAsync()
    {
        if (_detail is null) return;
        _previewing = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<EmailTemplatePreviewResult>>(
                "simfAccount.postJson",
                $"/account/api/admin/email/templates/{RouteType}/preview",
                new PreviewEmailTemplateRequest
                {
                    Subject = _subject,
                    BodyEn = _bodyEn,
                    BodyAr = _bodyAr,
                });
            if (env is { Success: true, Data: not null })
            {
                _preview = env.Data;
                _unknownTokens = env.Data.UnknownTokens;
                _error = null;
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.EmailTemplates.LoadFailed"];
            }
        }
        finally { _previewing = false; }
    }

    private async Task SaveAsync()
    {
        if (Working || _detail is null) return;

        // Re-preview first so the unknown-token gate always reflects the exact
        // copy being saved, regardless of event ordering.
        await PreviewAsync();
        if (_unknownTokens.Count > 0) return;

        _busy = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminEmailTemplateDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/email/templates/{RouteType}",
                new UpdateEmailTemplateRequest
                {
                    Subject = _subject,
                    BodyEn = _bodyEn,
                    BodyAr = _bodyAr,
                    IsActive = _isActive,
                });
            if (env is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(env.Data);
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.EmailTemplates.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    private async Task ConfirmResetAsync()
    {
        if (_busy || _detail is null) return;
        _busy = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminEmailTemplateDetail>>(
                "simfAccount.postJson",
                $"/account/api/admin/email/templates/{RouteType}/reset",
                null);
            _confirmingReset = false;
            if (env is { Success: true, Data: not null })
            {
                LoadFromDetail(env.Data);
                await OnReset.InvokeAsync(env.Data);
                await PreviewAsync();
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.EmailTemplates.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }
}
