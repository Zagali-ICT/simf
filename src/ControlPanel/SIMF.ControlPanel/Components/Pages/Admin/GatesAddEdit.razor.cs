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

    /// <summary>BUG-018 (18-7) — one page of candidate operators. The picker is
    /// server-searched instead of a blind top-200.</summary>
    private const int OperatorPageSize = 25;

    private IReadOnlyList<ProfileTypeOption> _profileTypes = Array.Empty<ProfileTypeOption>();
    private IReadOnlyList<OperatorOption> _operators = Array.Empty<OperatorOption>();
    private IReadOnlyList<HallOption> _halls = Array.Empty<HallOption>();

    private string _operatorSearch = string.Empty;
    private int _operatorTotal;
    private bool _loadingOperators;
    private string? _lookupError;

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

        // BUG-018 (18-4) — the allowed-profile-type + hall options now come from the
        // gate module's own Gates.Manage-gated lookup, not the ProfileTypes.View /
        // Halls.View admin lists. A Security-team gate manager used to see two
        // silently empty dropdowns because those lookups 403'd with no else branch.
        var optionsEnvelope = await JS.InvokeAsync<ApiResult<AdminGateFormOptions>>(
            "simfAccount.getJson", "/account/api/admin/gates/form-options");
        if (optionsEnvelope is { Success: true, Data: { } options })
        {
            _profileTypes = options.ProfileTypes
                .Select(option => new ProfileTypeOption(option.Id, option.Name)).ToList();
            _halls = options.Halls
                .Select(option => new HallOption(option.Id, option.Name)).ToList();
        }
        else
        {
            _lookupError = optionsEnvelope?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Gates.Lookup.Failed"];
        }

        await LoadOperatorsAsync();
    }

    /// <summary>BUG-018 (18-1 / 18-7) — the candidate operators are the users who
    /// can actually work a gate (approved app accounts on an operational profile
    /// type), NOT Control-Panel admin accounts. Searchable and paged server-side;
    /// a failure is surfaced instead of leaving an empty list unexplained.</summary>
    private async Task LoadOperatorsAsync()
    {
        if (_loadingOperators) return;
        _loadingOperators = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminGateOperatorCandidate>>>(
                "simfAccount.postJson", "/account/api/admin/gates/operator-candidates/list",
                new GridQuery
                {
                    Top = OperatorPageSize,
                    Search = string.IsNullOrWhiteSpace(_operatorSearch)
                        ? null : _operatorSearch.Trim(),
                });
            if (envelope is { Success: true, Data: { } page })
            {
                _operators = page.Items
                    .Select(candidate => new OperatorOption(
                        candidate.UserId, candidate.Email,
                        candidate.DisplayName, candidate.ProfileTypeName))
                    .ToList();
                _operatorTotal = page.Total;
            }
            else
            {
                _operators = Array.Empty<OperatorOption>();
                _operatorTotal = 0;
                _lookupError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Gates.Lookup.Failed"];
            }
        }
        finally { _loadingOperators = false; }
    }

    private void OnHallChanged(ChangeEventArgs e)
    {
        _model.HallId = e.Value is string s && Guid.TryParse(s, out var g) ? g : (Guid?)null;
    }

    // Checkbox lists toggle a single id in/out of the selection set — clearer
    // than a native <select multiple> and keeps the exact Guid set the API expects.
    private void ToggleAllowed(Guid id, bool selected)
    {
        if (selected) _model.AllowedProfileTypeIds.Add(id);
        else _model.AllowedProfileTypeIds.Remove(id);
    }

    private void ToggleOperator(Guid id, bool selected)
    {
        if (selected) _model.AssignedOperatorUserIds.Add(id);
        else _model.AssignedOperatorUserIds.Remove(id);
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
    private sealed record OperatorOption(
        Guid Id, string Email, string DisplayName, string ProfileTypeName);
    private sealed record HallOption(Guid Id, string Name);
}
