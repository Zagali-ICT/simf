using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class RolesAddEdit
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
            _model.Name = Initial.Name;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        var name = (_model.Name ?? string.Empty).Trim();
        if (name.Length is < 1 or > 64)
        {
            _error = L["Admin.Roles.Field.NameInvalid"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminRoleSummary>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminRoleSummary>>(
                    "simfAccount.postJson", "/account/api/admin/roles",
                    new AdminCreateRoleRequest { Name = name });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminRoleSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/roles/{Initial!.Id}",
                    new AdminUpdateRoleRequest { Name = name });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Roles.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Roles.Fallback"];
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
    }
}
