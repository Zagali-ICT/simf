using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

public partial class ProfileTypeForm
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _busy;
    private string? _success;
    private string? _error;
    private bool _isEdit;

    /// <summary>Optional existing row to pre-populate the form (Edit mode).
    /// When null, the form is in Create mode.</summary>
    [Parameter] public AdminProfileTypeSummary? Initial { get; set; }

    /// <summary>True for the partner-side host (OtherProfileTypesList),
    /// false for the audience-side host (VisitorProfileTypesList). Drives
    /// the IsVisitor payload on Create and the MobileAppRole picker
    /// visibility. Ignored in Edit mode (uses Initial.IsVisitor).</summary>
    [Parameter] public bool IsPartnerForm { get; set; }

    /// <summary>Raised after a successful create or update.</summary>
    [Parameter] public EventCallback<AdminProfileTypeSummary> OnSuccess { get; set; }

    /// <summary>Raised when the user clicks Cancel. If unset, no Cancel button renders.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    // Picker options. "Visitor" is omitted because the backend
    // rejects it (the Visitor app role is computed from UserType, not
    // assignable per ProfileType row). "Exhibitor" is listed (the
    // العارض app role: Visitor + lead-capture tools); without it an admin
    // could not assign the role AND editing the seeded Exhibitor row would
    // silently downgrade its MobileAppRole.
    private static readonly string[] MobileAppRoleOptions =
        new[] { "None", "Staff", "Moderator", "Exhibitor" };

    // The MobileAppRole picker is only meaningful on the
    // partner side (audience profile types resolve to MobileAppRole
    // .Visitor at JWT issue time). Edit uses the persisted IsVisitor
    // flag; Create uses the host-provided IsPartnerForm.
    private bool ShowMobileAppRolePicker =>
        _isEdit ? Initial?.IsVisitor == false : IsPartnerForm;

    // The "Show in Meet-People" toggle is only meaningful for partner
    // (Other) types, the pool the networking directory + recommender draw from;
    // gated exactly like the app-role picker (Edit reads the persisted
    // IsVisitor flag; Create reads the host page's IsPartnerForm).
    private bool ShowPartnerDirectoryToggle =>
        _isEdit ? Initial?.IsVisitor == false : IsPartnerForm;

    // The VIP-tier toggle is the mirror image: it is only meaningful for
    // AUDIENCE types (VVIP / VIP), the pool VIP-tier seat self-reservation
    // draws from. A partner type is never a VIP tier, so the box stays off
    // their form and the request carries false.
    private bool ShowVipTierToggle =>
        _isEdit ? Initial?.IsVisitor != false : !IsPartnerForm;

    protected override void OnInitialized()
    {
        _isEdit = Initial is not null;
        if (Initial is not null)
        {
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.PageColor = Initial.PageColor;
            _model.MobileAppRole = string.IsNullOrEmpty(Initial.MobileAppRole)
                ? "None" : Initial.MobileAppRole;
            _model.IsActive = Initial.IsActive;
            _model.IsAppRegisterable = Initial.IsAppRegisterable;
            _model.ShowInPartnerDirectory = Initial.ShowInPartnerDirectory;
            _model.IsVipTier = Initial.IsVipTier;
        }
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
    }

    private string SubmitLabel =>
        _isEdit ? L["Admin.ProfileTypes.Submit.Update"] : L["Admin.ProfileTypes.Submit.Create"];

    /// <summary>Localised display string. After the
    /// UserType collapse, every form is under the Visitor scope; the
    /// label now reflects the audience-vs-partner intent the host page
    /// set (Edit reads from Initial.IsVisitor; Create reads from
    /// IsPartnerForm).</summary>
    private string LocalisedUserType
    {
        get
        {
            var isAudience = _isEdit ? (Initial?.IsVisitor ?? true) : !IsPartnerForm;
            return isAudience
                ? L["Admin.ProfileTypes.Scope.Audience"]
                : L["Admin.ProfileTypes.Scope.Partner"];
        }
    }

    // Only feed the native colour picker when the model value is a
    // canonical #rrggbb hex. CSS variables and 3-digit hex shortcuts stay in
    // the text field as-is; the picker falls back to a brand-neutral default
    // for display purposes (no write happens until the user picks).
    private static readonly System.Text.RegularExpressions.Regex HexSixPattern =
        new("^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private string PageColorAsHex =>
        HexSixPattern.IsMatch(_model.PageColor) ? _model.PageColor : AdminFormDefaults.PageColor;

    // Mirror the seeder default in the CP create path: selecting an
    // operational app role (Staff / Moderator) auto-hides the type from the
    // app sign-up picker, so a hand-created CP-only type behaves like the
    // seeded one (which ships IsAppRegisterable=false). The admin can still
    // re-tick "Show in the app sign-up picker" deliberately; picking a
    // non-operational role never touches the box.
    private void OnMobileAppRoleChanged(string? value)
    {
        _model.MobileAppRole = string.IsNullOrEmpty(value) ? "None" : value;
        if (_model.MobileAppRole is "Staff" or "Moderator")
        {
            _model.IsAppRegisterable = false;
        }
    }

    private void OnPageColorTextInput(ChangeEventArgs e)
    {
        _model.PageColor = e.Value?.ToString() ?? string.Empty;
    }

    private void OnPageColorSwatchInput(ChangeEventArgs e)
    {
        var picked = e.Value?.ToString();
        if (!string.IsNullOrEmpty(picked))
        {
            _model.PageColor = picked;
        }
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _success = null;
        _error = null;

        if (!ValidateForm()) { return; }

        _busy = true;
        try
        {
            var envelope = _isEdit && Initial is not null
                ? await JS.InvokeAsync<ApiResult<AdminProfileTypeSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/profile-types/{Initial.Id}",
                    BuildUpdateRequest(Initial))
                : await JS.InvokeAsync<ApiResult<AdminProfileTypeSummary>>(
                    "simfAccount.postJson", "/account/api/admin/profile-types",
                    BuildCreateRequest());

            if (envelope is { Success: true, Data: not null })
            {
                _success = _isEdit
                    ? string.Format(L["Admin.ProfileTypes.Updated"], envelope.Data.Name)
                    : string.Format(L["Admin.ProfileTypes.Created"], envelope.Data.Name);
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ProfileTypes.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    /// <summary>Validates the form, setting <see cref="_error"/> to the first
    /// problem found.</summary>
    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        {
            _error = L["Admin.ProfileTypes.Field.NameInvalid"];
            return false;
        }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        {
            _error = L["Admin.ProfileTypes.Field.NameArabicInvalid"];
            return false;
        }
        if (string.IsNullOrWhiteSpace(_model.PageColor) || _model.PageColor.Length > 32)
        {
            _error = L["Admin.ProfileTypes.Field.PageColorInvalid"];
            return false;
        }
        return true;
    }

    /// <summary>MobileAppRole is only sent when the picker was shown
    /// (UserType=Other). Sending it for Visitor rows would be wire noise: the
    /// backend defaults to None anyway, and the claim is resolved from UserType
    /// regardless.</summary>
    private string? MobileAppRolePayload =>
        ShowMobileAppRolePicker ? _model.MobileAppRole : null;

    private AdminCreateProfileTypeRequest BuildCreateRequest() => new()
    {
        // Only the Visitor scope is accepted for non-admin profile types.
        // The audience-vs-partner split rides on IsVisitor.
        UserType = "Visitor",
        Name = _model.Name.Trim(),
        NameArabic = _model.NameArabic.Trim(),
        PageColor = _model.PageColor.Trim(),
        MobileAppRole = MobileAppRolePayload,
        IsActive = _model.IsActive,
        IsVisitor = !IsPartnerForm,
        // App sign-up picker visibility toggle.
        IsAppRegisterable = _model.IsAppRegisterable,
        // Meet-People networking visibility toggle.
        ShowInPartnerDirectory = _model.ShowInPartnerDirectory,
        // VIP audience tier. Always sent: the request defaults it to false, so
        // omitting it would clear the tier rather than leave it alone.
        IsVipTier = _model.IsVipTier,
    };

    private AdminUpdateProfileTypeRequest BuildUpdateRequest(AdminProfileTypeSummary initial) => new()
    {
        Name = _model.Name.Trim(),
        NameArabic = _model.NameArabic.Trim(),
        PageColor = _model.PageColor.Trim(),
        MobileAppRole = MobileAppRolePayload,
        IsActive = _model.IsActive,
        // IsVisitor is mutable on update — keeps the row's existing
        // audience/partner flag unless the host explicitly changes it.
        IsVisitor = initial.IsVisitor,
        // App sign-up picker visibility toggle.
        IsAppRegisterable = _model.IsAppRegisterable,
        // Meet-People networking visibility toggle.
        ShowInPartnerDirectory = _model.ShowInPartnerDirectory,
        // VIP audience tier — see BuildCreateRequest on why it is always sent.
        IsVipTier = _model.IsVipTier,
    };

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string PageColor { get; set; } = AdminFormDefaults.PageColor;
        // Defaults to "None" (the safe baseline); admin opts in
        // by picking Staff or Moderator.
        public string MobileAppRole { get; set; } = "None";
        public bool IsActive { get; set; } = true;
        // App sign-up picker visibility; default true (visible).
        public bool IsAppRegisterable { get; set; } = true;
        // "Meet People" networking visibility; default true (shown).
        public bool ShowInPartnerDirectory { get; set; } = true;
        // VIP audience tier; default false — a new type is not VIP until an
        // admin ticks it.
        public bool IsVipTier { get; set; }
    }
}
