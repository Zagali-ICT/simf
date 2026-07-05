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
using SIMF.Contracts.Contacts;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ContactPicker
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed record Selection(Guid Id, string NameAr, string? NameEn);

    [Parameter] public Guid? Value { get; set; }
    [Parameter] public EventCallback<Guid?> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }

    private string _search = string.Empty;
    private bool _searching;
    private bool _searched;
    private List<ContactPickerItem> _results = new();
    private Selection? _selected;
    private Guid? _resolvedFor;

    // Resolve the current selection's name when the form opens with a ContactId
    // already set (edit). Guarded so it only fetches when Value actually changes.
    protected override async Task OnParametersSetAsync()
    {
        if (Value == _resolvedFor) { return; }
        _resolvedFor = Value;
        if (Value is null)
        {
            _selected = null;
            return;
        }
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminContactDetail>>(
                "simfAccount.getJson", $"/account/api/admin/contacts/{Value}");
            _selected = env is { Success: true, Data: not null }
                ? new Selection(env.Data.Id, env.Data.NameAr, env.Data.NameEn)
                : null;
        }
        catch
        {
            _selected = null;
        }
    }

    private async Task SearchAsync()
    {
        if (_searching) { return; }
        _searching = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<IReadOnlyList<ContactPickerItem>>>(
                "simfAccount.getJson",
                $"/account/api/admin/contacts/picker?search={Uri.EscapeDataString(_search ?? string.Empty)}");
            _results = env is { Success: true, Data: not null }
                ? env.Data.ToList()
                : new List<ContactPickerItem>();
            _searched = true;
        }
        finally { _searching = false; }
    }

    private async Task SelectAsync(ContactPickerItem item)
    {
        _selected = new Selection(item.Id, item.NameAr, item.NameEn);
        _resolvedFor = item.Id;
        _results = new List<ContactPickerItem>();
        _searched = false;
        _search = string.Empty;
        await ValueChanged.InvokeAsync(item.Id);
    }

    private async Task ClearAsync()
    {
        _selected = null;
        _resolvedFor = null;
        await ValueChanged.InvokeAsync(null);
    }

    private static string SelectedLabel(Selection s) =>
        string.IsNullOrWhiteSpace(s.NameEn) ? s.NameAr : $"{s.NameAr} · {s.NameEn}";

    private static string ItemLabel(ContactPickerItem item) =>
        string.IsNullOrWhiteSpace(item.NameEn) ? item.NameAr : $"{item.NameAr} · {item.NameEn}";
}
