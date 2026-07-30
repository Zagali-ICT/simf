using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiDashboard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private AdminAiDashboard? _dashboard;
    private bool _loading;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminAiDashboard>>(
                "simfAccount.getJson", "/account/api/admin/ai/dashboard");
            if (env is { Success: true, Data: not null })
            {
                _dashboard = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiDashboard.LoadFailed"]);
            }
        }
        catch
        {
            // A transport / JS-interop throw (not a non-success envelope) surfaces
            // the localized failure toast instead of an unhandled error, matching
            // AttendanceDashboard's load handler.
            _toast = new Toast("error", L["Admin.AiDashboard.LoadFailed"]);
        }
        finally { _loading = false; }
    }
}
