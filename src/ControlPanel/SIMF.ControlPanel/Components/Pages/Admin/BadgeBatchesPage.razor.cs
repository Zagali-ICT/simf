using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BadgeBatchesPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminBadgeBatchSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // Re-email modal — the batch being emailed + the editable organiser address
    // (pre-filled with the batch's last recipient).
    private AdminBadgeBatchSummary? _reEmailTarget;
    private string _reEmailRecipient = string.Empty;

    // Revoke confirm modal — the batch about to be disabled.
    private AdminBadgeBatchSummary? _revokeTarget;

    // Top-up modal — the order being added to, plus the tier and how many.
    // The type list is loaded once on init rather than per open: it is a small
    // lookup, and fetching it inside the click would leave the picker empty for
    // the first render of the modal.
    private AdminBadgeBatchSummary? _topUpTarget;
    private List<AdminProfileTypeSummary> _profileTypes = new();
    private AdminProfileTypeSummary? _topUpType;
    private string _topUpCount = string.Empty;
    private string? _topUpError;
    private string? _profileTypesError;

    /// <summary>True when there is no tier to pick, so the dialog says why
    /// instead of refusing every press of its own confirm button.</summary>
    private bool NoTiersToPick => _profileTypes.Count == 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await LoadProfileTypesAsync();
    }

    /// <summary>Visitor tiers a badge can be minted for. Mirrors the bulk
    /// generator's filter — a non-visitor type must never reach a bulk order,
    /// because a bulk-approved badge of an elevated role would hand out
    /// QR-accessible authority.</summary>
    private async Task LoadProfileTypesAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", "/account/api/admin/profile-types?userType=Visitor");
        if (envelope is { Success: true, Data: not null })
        {
            _profileTypes = envelope.Data.Where(p => p.IsActive && p.IsVisitor).ToList();
            return;
        }

        // Kept rather than swallowed. This lookup is gated on ProfileTypes.View,
        // which is NOT the permission that renders the top-up button, so a role
        // holding one without the other would otherwise open a dialog whose
        // picker is empty and whose only response is "choose a profile type".
        _profileTypesError = envelope?.Error?.MessageForCurrentCulture()
            ?? L["Admin.BadgeBatches.LoadFailed"];
    }

    private static string TypeLabel(AdminProfileTypeSummary profileType) =>
        InReadingLanguage(profileType.Name, profileType.NameArabic);

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    /// <summary>A bilingual pair shown in the reading language, falling back to
    /// the other side so nothing renders blank while one is still
    /// untranslated. Used for both the order name and the profile-type label,
    /// which had the same four lines twice.</summary>
    private static string InReadingLanguage(string name, string nameArabic) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? (string.IsNullOrWhiteSpace(nameArabic) ? name : nameArabic)
            : (string.IsNullOrWhiteSpace(name) ? nameArabic : name);

    private static string OrderName(AdminBadgeBatchSummary row) =>
        InReadingLanguage(row.Name, row.NameArabic);

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BadgeBatches.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OpenReEmail(AdminBadgeBatchSummary row)
    {
        _reEmailTarget = row;
        _reEmailRecipient = row.RecipientEmail ?? string.Empty;
        _toast = null;
    }

    private async Task ReEmailAsync()
    {
        if (_reEmailTarget is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminReEmailBadgeBatchResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/re-email",
                new AdminReEmailBadgeBatchRequest
                {
                    BatchId = _reEmailTarget.Id,
                    RecipientEmail = _reEmailRecipient.Trim(),
                });
            if (env is { Success: true, Data: not null })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.BadgeBatches.ReEmail.Done"],
                        env.Data.BadgeCount, _reEmailRecipient.Trim()));
                _reEmailTarget = null;
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BadgeBatches.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OpenTopUp(AdminBadgeBatchSummary row)
    {
        _topUpTarget = row;
        _topUpType = null;
        _topUpCount = string.Empty;
        _topUpError = null;
        _toast = null;
    }

    private void CloseTopUp()
    {
        _topUpTarget = null;
        _topUpError = null;
    }

    private async Task TopUpAsync()
    {
        if (_topUpTarget is null || _busy) return;

        // Checked before posting, and reported INSIDE the dialog, so the
        // correction is made while the fields are still in front of the operator.
        if (_topUpType is null)
        {
            _topUpError = L["Admin.BadgeBatches.TopUp.TypeRequired"];
            return;
        }
        if (!int.TryParse(_topUpCount?.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out var count)
            || count < 1)
        {
            _topUpError = L["Admin.BadgeBatches.TopUp.CountInvalid"];
            return;
        }

        _busy = true;
        _topUpError = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminTopUpBadgeBatchResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/top-up",
                new AdminTopUpBadgeBatchRequest
                {
                    BatchId = _topUpTarget.Id,
                    Batches = new List<BulkBadgeBatch>
                    {
                        new() { ProfileTypeId = _topUpType.Id, Count = count },
                    },
                });
            if (env is { Success: true, Data: not null })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.BadgeBatches.TopUp.Done"],
                        env.Data.Added, env.Data.TotalCount));
                CloseTopUp();
                await LoadAsync();
            }
            else
            {
                // The server refuses a revoked order and the direct-registration
                // order. The dialog stays OPEN so the refusal is read where the
                // request was made.
                _topUpError = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.BadgeBatches.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    private void OpenRevoke(AdminBadgeBatchSummary row)
    {
        _revokeTarget = row;
        _toast = null;
    }

    private async Task RevokeAsync()
    {
        if (_revokeTarget is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminRevokeBadgeBatchResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/revoke",
                new AdminRevokeBadgeBatchRequest { BatchId = _revokeTarget.Id });
            if (env is { Success: true, Data: not null })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.BadgeBatches.Revoke.Done"], env.Data.RevokedCount));
                _revokeTarget = null;
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BadgeBatches.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
