using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BulkBadgeGenerator
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>When true (the Delegates desk), the generated badges are flagged
    /// as delegates by default. The Visitors desk leaves it off.</summary>
    [Parameter] public bool DefaultIsDelegate { get; set; }

    /// <summary>Show the section title + hint. Off when the host frames the
    /// generator in a dialog that already carries a title.</summary>
    [Parameter] public bool ShowHeader { get; set; } = true;

    private record Toast(string Variant, string Message);

    // One profile-type + count line in the batch the admin is building.
    private sealed class BatchRow
    {
        public Guid TypeId { get; }
        public string TypeName { get; }
        public string? Color { get; }
        public int Count { get; set; }

        public BatchRow(Guid typeId, string typeName, string? color, int count)
        {
            TypeId = typeId;
            TypeName = typeName;
            Color = color;
            Count = count;
        }
    }

    private List<AdminProfileTypeSummary> _profileTypes = new();
    private readonly List<BatchRow> _batch = new();
    private AdminProfileTypeSummary? _selectedType;
    private string _countInput = string.Empty;
    private bool _bulkIsDelegate;
    private bool _busy;
    private Toast? _bulkToast;

    // Who the order is for. An order identified only by its counts is
    // unrecognisable in the list as soon as two of them are the same size.
    private string _bulkName = string.Empty;
    private string _bulkNameArabic = string.Empty;

    // Confirm modal + optional organiser recipient for the emailed
    // QR-badge ZIP.
    private bool _confirmOpen;
    private string _recipientEmail = string.Empty;
    private string _confirmCountsText = string.Empty;

    private int BatchTotal => _batch.Sum(b => b.Count);

    protected override async Task OnInitializedAsync()
    {
        _bulkIsDelegate = DefaultIsDelegate;
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", "/account/api/admin/profile-types?userType=Visitor");
        if (envelope is { Success: true, Data: not null })
        {
            // Bulk-generate is for audience tiers only (mirrors the API guard).
            _profileTypes = envelope.Data.Where(p => p.IsActive && p.IsVisitor).ToList();
        }
    }

    private static string TypeLabel(AdminProfileTypeSummary pt) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? pt.NameArabic : pt.Name;

    // Add the picked type + count to the batch. Adding a type already in the
    // batch merges into the existing row (one line per type). Rejects an empty
    // pick or a non-positive count.
    private void AddToBatch()
    {
        if (_selectedType is null || !int.TryParse(_countInput, out var count) || count <= 0)
        {
            _bulkToast = new Toast("error", L["Admin.Delegates.Bulk.PickType"]);
            return;
        }
        var existing = _batch.FirstOrDefault(b => b.TypeId == _selectedType.Id);
        if (existing is not null)
        {
            existing.Count += count;
        }
        else
        {
            _batch.Add(new BatchRow(
                _selectedType.Id, TypeLabel(_selectedType), _selectedType.PageColor, count));
        }
        _selectedType = null;
        _countInput = string.Empty;
        _bulkToast = null;
    }

    private void RemoveFromBatch(Guid typeId) => _batch.RemoveAll(b => b.TypeId == typeId);

    // Build the human summary, then open the confirm modal (where the optional
    // organiser email is entered).
    private void OpenConfirm()
    {
        if (_batch.Count == 0)
        {
            _bulkToast = new Toast("error", L["Admin.Delegates.Bulk.PickType"]);
            return;
        }
        // × = the multiplication sign (×), kept per the house doc rule.
        _confirmCountsText = string.Join(" + ", _batch.Select(b => $"{b.TypeName} × {b.Count}"));
        _bulkToast = null;
        _confirmOpen = true;
    }

    private void CloseConfirm() => _confirmOpen = false;

    private async Task GenerateBulkAsync()
    {
        // Re-entrancy guard (parity with WalkInRegistrationForm.HandleSubmitAsync):
        // a fast double-click on a slow circuit can fire before Loading disables it.
        if (_busy) return;

        var batches = _batch
            .Select(b => new BulkBadgeBatch { ProfileTypeId = b.TypeId, Count = b.Count })
            .ToList();
        if (batches.Count == 0)
        {
            _bulkToast = new Toast("error", L["Admin.Delegates.Bulk.PickType"]);
            _confirmOpen = false;
            return;
        }

        // The order needs a name in both languages. Checked here as well as on the
        // server so the operator is told before the confirm modal commits.
        if (string.IsNullOrWhiteSpace(_bulkName) || string.IsNullOrWhiteSpace(_bulkNameArabic))
        {
            _bulkToast = new Toast("error", L["Admin.Delegates.Bulk.NameRequired"]);
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
                    Name = _bulkName.Trim(),
                    NameArabic = _bulkNameArabic.Trim(),
                    IsDelegate = _bulkIsDelegate,
                    Batches = batches,
                    RecipientEmail = recipient,
                });
            if (envelope is { Success: true, Data: not null })
            {
                _bulkToast = new Toast("success", envelope.Data.EmailQueued
                    ? string.Format(L["Admin.Delegates.Bulk.ResultEmailed"], envelope.Data.Created, recipient)
                    : string.Format(L["Admin.Delegates.Bulk.Result"], envelope.Data.Created));
                _batch.Clear();
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
}
