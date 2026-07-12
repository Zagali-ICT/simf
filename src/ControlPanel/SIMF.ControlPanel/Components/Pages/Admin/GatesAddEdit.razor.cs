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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class GatesAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    private IReadOnlyList<ProfileTypeOption> _profileTypes = Array.Empty<ProfileTypeOption>();
    private IReadOnlyList<OperatorOption> _operators = Array.Empty<OperatorOption>();
    private IReadOnlyList<HallOption> _halls = Array.Empty<HallOption>();

    protected override async Task OnInitializedAsync()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.Description = Initial.Description ?? string.Empty;
            _model.DescriptionArabic = Initial.DescriptionArabic ?? string.Empty;
            _model.DirectionMode = Initial.DirectionMode;
            _model.IsActive = Initial.IsActive;
            _model.AllowedProfileTypeIds = new HashSet<Guid>(Initial.AllowedProfileTypeIds);
            _model.AssignedOperatorUserIds = new HashSet<Guid>(Initial.AssignedOperatorUserIds);
            _model.HallId = Initial.HallId;
        }
        _editContext = new EditContext(_model);

        // Load active profile types + active admins as candidate operators.
        var ptEnvelope = await JS.InvokeAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>(
            "simfAccount.postJson", "/account/api/admin/profile-types/list",
            new GridQuery { Top = 200, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
        if (ptEnvelope is { Success: true, Data.Items: { } items })
        {
            _profileTypes = items.Select(i => new ProfileTypeOption(i.Id, i.Name)).ToList();
        }

        var opsEnvelope = await JS.InvokeAsync<ApiResult<GridPage<AdminUserSummary>>>(
            "simfAccount.postJson", "/account/api/admin/admins/list",
            new GridQuery { Top = 200 });
        if (opsEnvelope is { Success: true, Data.Items: { } admins })
        {
            _operators = admins
                .Select(a => new OperatorOption(a.Id, a.Email, a.DisplayName ?? string.Empty))
                .ToList();
        }

        // X-1 — load active halls for the optional hall-door binding, same list
        // endpoint the Booths/Sessions forms use. Empty option maps to null
        // (perimeter gate).
        var hallsEnvelope = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list",
            new GridQuery { Top = 500 });
        if (hallsEnvelope is { Success: true, Data.Items: { } halls })
        {
            _halls = halls.Where(h => h.IsActive)
                .OrderBy(h => h.Name)
                .Select(h => new HallOption(h.Id, h.Name)).ToList();
        }
    }

    private void OnHallChanged(ChangeEventArgs e)
    {
        _model.HallId = e.Value is string s && Guid.TryParse(s, out var g) ? g : (Guid?)null;
    }

    private void OnAllowedChanged(ChangeEventArgs e)
    {
        if (e.Value is string[] values)
        {
            _model.AllowedProfileTypeIds = values
                .Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue).Select(g => g!.Value).ToHashSet();
        }
    }

    private void OnOperatorsChanged(ChangeEventArgs e)
    {
        if (e.Value is string[] values)
        {
            _model.AssignedOperatorUserIds = values
                .Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue).Select(g => g!.Value).ToHashSet();
        }
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Code) || _model.Code.Length is < 2 or > 16)
        { _error = L["Admin.Gates.Field.CodeInvalid"]; return; }
        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        { _error = L["Admin.Gates.Field.NameInvalid"]; return; }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        { _error = L["Admin.Gates.Field.NameArabicInvalid"]; return; }

        _busy = true;
        try
        {
            ApiResult<AdminGateDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminGateDetail>>(
                    "simfAccount.postJson", "/account/api/admin/gates",
                    new AdminCreateGateRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        Description = string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                        DescriptionArabic = string.IsNullOrWhiteSpace(_model.DescriptionArabic) ? null : _model.DescriptionArabic.Trim(),
                        DirectionMode = _model.DirectionMode,
                        HallId = _model.HallId,
                        AllowedProfileTypeIds = _model.AllowedProfileTypeIds.ToList(),
                        AssignedOperatorUserIds = _model.AssignedOperatorUserIds.ToList(),
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminGateDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/gates/{Initial!.Id}",
                    new AdminUpdateGateRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        Description = string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                        DescriptionArabic = string.IsNullOrWhiteSpace(_model.DescriptionArabic) ? null : _model.DescriptionArabic.Trim(),
                        DirectionMode = _model.DirectionMode,
                        IsActive = _model.IsActive,
                        HallId = _model.HallId,
                        AllowedProfileTypeIds = _model.AllowedProfileTypeIds.ToList(),
                        AssignedOperatorUserIds = _model.AssignedOperatorUserIds.ToList(),
                    });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.Gates.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Gates.Fallback"];
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionArabic { get; set; } = string.Empty;
        public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;
        public bool IsActive { get; set; } = true;
        public HashSet<Guid> AllowedProfileTypeIds { get; set; } = new();
        public HashSet<Guid> AssignedOperatorUserIds { get; set; } = new();
        public Guid? HallId { get; set; }
    }

    private sealed record ProfileTypeOption(Guid Id, string Name);
    private sealed record OperatorOption(Guid Id, string Email, string DisplayName);
    private sealed record HallOption(Guid Id, string Name);
}
