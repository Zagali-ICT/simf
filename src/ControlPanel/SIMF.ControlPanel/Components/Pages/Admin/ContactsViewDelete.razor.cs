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
using SIMF.Contracts.Faq;
using SIMF.Contracts.Contacts;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ContactsViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    private string DisplayName
    {
        get
        {
            if (Initial is null) return string.Empty;
            var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
            if (!isArabic && !string.IsNullOrWhiteSpace(Initial.NameEn)) return Initial.NameEn!;
            return Initial.NameAr;
        }
    }

    private string CountryName
    {
        get
        {
            if (Initial is null) return "—";
            var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
            var name = isArabic ? Initial.CountryNameAr : Initial.CountryNameEn;
            return string.IsNullOrWhiteSpace(name) ? "—" : name;
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/contacts/{Initial.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                // Close the confirm first so the error lands on the visible
                // form body, not behind the (still-open) confirm overlay.
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Contacts.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Contacts.LoadFailed"];
        }
        finally { _busy = false; }
    }
}
