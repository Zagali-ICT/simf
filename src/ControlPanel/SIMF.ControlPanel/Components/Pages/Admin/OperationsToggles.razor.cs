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

    /// <summary>§6.16 (F-U5-007) — true only while a load is actually in
    /// flight. Both sections used to key their "Loading…" text off a null
    /// state, so a load that FAILED left the page reading "Loading…" forever.</summary>
    private bool _loading;

    /// <summary>The per-section load error, when the section could not be
    /// fetched. Rendered in place of the section with a Retry.</summary>
    private string? _gateError;
    private string? _archiveError;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _gateError = null;
        _archiveError = null;
        try
        {
            // Note: simfReadEnvelope turns a transport/HTTP failure into a
            // RETURNED ApiResult.Fail rather than a throw, so the catch below
            // never sees the common failure — each envelope must be checked.
            var gateEnv = await JS.InvokeAsync<ApiResult<RegistrationGateState>>(
                "simfAccount.getJson", "/account/api/admin/registration-gate");
            if (gateEnv is { Success: true, Data: not null })
            {
                _gate = gateEnv.Data;
                _gateIsOpen = _gate.IsOpen;
                _gateAutoCloseInput = _gate.AutoClose?.ToSaudi()
                    .ToString("yyyy-MM-ddTHH:mm") ?? string.Empty;
            }
            else
            {
                _gateError = gateEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Operations.LoadFailed"];
            }

            var archiveEnv = await JS.InvokeAsync<ApiResult<ArchiveVisibilityState>>(
                "simfAccount.getJson", "/account/api/admin/archive/visibility");
            if (archiveEnv is { Success: true, Data: not null })
            {
                _archive = archiveEnv.Data;
                _archiveIsVisible = _archive.IsVisible;
            }
            else
            {
                _archiveError = archiveEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Operations.LoadFailed"];
            }
        }
        catch
        {
            _gateError ??= L["Admin.Operations.LoadFailed"];
            _archiveError ??= L["Admin.Operations.LoadFailed"];
        }
        finally { _loading = false; }
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
                autoClose = SaudiTime.FromSaudiWallClock(parsed);
            }
            var env = await JS.InvokeAsync<ApiResult<RegistrationGateState>>(
                "simfAccount.putJson", "/account/api/admin/registration-gate",
                new UpdateRegistrationGateRequest
                {
                    IsOpen = _gateIsOpen,
                    AutoClose = autoClose,
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
