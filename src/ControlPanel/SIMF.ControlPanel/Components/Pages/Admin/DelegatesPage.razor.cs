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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class DelegatesPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private AdminWalkInRegistrationResponse? _lastResponse;
    private Guid _formKey = Guid.NewGuid();

    private List<AdminProfileTypeSummary> _profileTypes = new();
    private readonly Dictionary<Guid, int> _counts = new();
    private bool _bulkIsDelegate = true;
    private bool _busy;
    private Toast? _bulkToast;

    // D-751 (#10) — confirm modal + optional organiser recipient for the emailed
    // QR-badge ZIP.
    private bool _confirmOpen;
    private string _recipientEmail = string.Empty;
    private string _confirmCountsText = string.Empty;

    private int ConfirmTotal => _counts.Where(c => c.Value > 0).Sum(c => c.Value);

    protected override async Task OnInitializedAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", "/account/api/admin/profile-types?userType=Visitor");
        if (envelope is { Success: true, Data: not null })
        {
            // Bulk-generate is for audience tiers only (mirrors the API guard).
            _profileTypes = envelope.Data.Where(p => p.IsActive && p.IsVisitor).ToList();
        }
    }

    private string CountFor(Guid id) => _counts.TryGetValue(id, out var n) ? n.ToString() : "0";

    private void SetCount(Guid id, string value) =>
        _counts[id] = int.TryParse(value, out var n) && n > 0 ? n : 0;

    private string NameFor(Guid id) =>
        _profileTypes.FirstOrDefault(p => p.Id == id)?.Name ?? id.ToString();

    // Validate at least one count is picked, build the human summary, then open the
    // confirm modal (where the optional organiser email is entered).
    private void OpenConfirm()
    {
        var picked = _counts.Where(c => c.Value > 0).ToList();
        if (picked.Count == 0)
        {
            _bulkToast = new Toast("error", L["Admin.Delegates.Bulk.PickCount"]);
            return;
        }
        // × = the multiplication sign (×), kept per the house doc rule.
        _confirmCountsText = string.Join(" + ",
            picked.Select(c => $"{NameFor(c.Key)} × {c.Value}"));
        _bulkToast = null;
        _confirmOpen = true;
    }

    private void CloseConfirm() => _confirmOpen = false;

    private async Task GenerateBulkAsync()
    {
        var batches = _counts
            .Where(c => c.Value > 0)
            .Select(c => new BulkBadgeBatch { ProfileTypeId = c.Key, Count = c.Value })
            .ToList();
        if (batches.Count == 0)
        {
            _bulkToast = new Toast("error", L["Admin.Delegates.Bulk.PickCount"]);
            _confirmOpen = false;
            return;
        }

        var recipient = string.IsNullOrWhiteSpace(_recipientEmail) ? null : _recipientEmail.Trim();

        _busy = true;
        _bulkToast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminBulkGenerateBadgesResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/bulk-generate",
                new AdminBulkGenerateBadgesRequest
                {
                    IsDelegate = _bulkIsDelegate,
                    Batches = batches,
                    RecipientEmail = recipient,
                });
            if (envelope is { Success: true, Data: not null })
            {
                _bulkToast = new Toast("success", envelope.Data.EmailQueued
                    ? string.Format(L["Admin.Delegates.Bulk.ResultEmailed"], envelope.Data.Created, recipient)
                    : string.Format(L["Admin.Delegates.Bulk.Result"], envelope.Data.Created));
                _counts.Clear();
                _recipientEmail = string.Empty;
            }
            else
            {
                _bulkToast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.Delegates.Bulk.Failed"]);
            }
            _confirmOpen = false;
        }
        finally { _busy = false; }
    }

    private void OnSingleSuccess(AdminWalkInRegistrationResponse response) => _lastResponse = response;

    private void OnSuccessModalClose() => _lastResponse = null;

    private async Task OnPrintAsync() => await JS.InvokeVoidAsync("window.print");

    private void OnRegisterAnother()
    {
        _lastResponse = null;
        _formKey = Guid.NewGuid();
        StateHasChanged();
    }
}
