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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OperationsToggles
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private RegistrationGateState? _gate;
    private ArchiveVisibilityState? _archive;
    private bool _gateIsOpen;
    private string _gateAutoCloseInput = string.Empty;
    private bool _archiveIsVisible;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var gateEnv = await JS.InvokeAsync<ApiResult<RegistrationGateState>>(
                "simfAccount.getJson", "/account/api/admin/registration-gate");
            if (gateEnv is { Success: true, Data: not null })
            {
                _gate = gateEnv.Data;
                _gateIsOpen = _gate.IsOpen;
                _gateAutoCloseInput = _gate.AutoCloseUtc?.UtcDateTime
                    .ToString("yyyy-MM-ddTHH:mm") ?? string.Empty;
            }

            var archiveEnv = await JS.InvokeAsync<ApiResult<ArchiveVisibilityState>>(
                "simfAccount.getJson", "/account/api/admin/archive/visibility");
            if (archiveEnv is { Success: true, Data: not null })
            {
                _archive = archiveEnv.Data;
                _archiveIsVisible = _archive.IsVisible;
            }
        }
        catch
        {
            _toast = new Toast("error", L["Admin.Operations.LoadFailed"]);
        }
    }

    private async Task SaveGateAsync()
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            DateTimeOffset? autoClose = null;
            if (!string.IsNullOrWhiteSpace(_gateAutoCloseInput))
            {
                if (!DateTime.TryParse(_gateAutoCloseInput, out var parsed))
                {
                    _toast = new Toast("error", L["Admin.Operations.RegistrationGate.AutoCloseInvalid"]);
                    return;
                }
                autoClose = new DateTimeOffset(
                    DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
            }
            var env = await JS.InvokeAsync<ApiResult<RegistrationGateState>>(
                "simfAccount.putJson", "/account/api/admin/registration-gate",
                new UpdateRegistrationGateRequest
                {
                    IsOpen = _gateIsOpen,
                    AutoCloseUtc = autoClose,
                });
            if (env is { Success: true, Data: not null })
            {
                _gate = env.Data;
                _toast = new Toast("success", L["Admin.Operations.RegistrationGate.Saved"]);
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Operations.SaveFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task SaveArchiveAsync()
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<ArchiveVisibilityState>>(
                "simfAccount.putJson", "/account/api/admin/archive/visibility",
                new UpdateArchiveVisibilityRequest { IsVisible = _archiveIsVisible });
            if (env is { Success: true, Data: not null })
            {
                _archive = env.Data;
                _toast = new Toast("success", L["Admin.Operations.Archive.Saved"]);
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Operations.SaveFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
