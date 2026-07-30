using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class WalkInSuccessModal
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    private string _qrSvg = string.Empty;

    // D-425 — a pending walk-in carries no QR id yet (minted on approval).
    private bool Pending => Response is not null && string.IsNullOrEmpty(Response.QrId);

    /// <summary>The walk-in response — when null the modal is hidden.</summary>
    [Parameter] public AdminWalkInRegistrationResponse? Response { get; set; }

    /// <summary>Fires when the modal is dismissed (Close / overlay / Esc).</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Fires when the desk clicks "Register another" — the parent
    /// typically resets the form and closes this modal.</summary>
    [Parameter] public EventCallback OnRegisterAnother { get; set; }

    /// <summary>Fires when the desk clicks Print. The parent invokes
    /// <c>window.print</c> via JS (kept out of this component so the
    /// JS-runtime dependency stays on the host page).</summary>
    [Parameter] public EventCallback OnPrint { get; set; }

    protected override void OnParametersSet()
    {
        if (Response is not null && !string.IsNullOrEmpty(Response.QrId))
        {
            _qrSvg = BadgeQrCode.ToSvg(Response.QrId);
        }
        else
        {
            _qrSvg = string.Empty;
        }
    }

    private string TypeLabel
    {
        get
        {
            if (Response is null) { return string.Empty; }
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                ? Response.ProfileTypeNameArabic
                : Response.ProfileTypeName;
        }
    }

    private string EmailLabel
    {
        get
        {
            if (Response is null) { return string.Empty; }
            // The placeholder synthetic email shouldn't be displayed to staff.
            if (Response.Email.EndsWith("@simf.local", StringComparison.OrdinalIgnoreCase))
            {
                return L["Admin.WalkIn.Success.NoEmail"];
            }
            return Response.Email;
        }
    }

}
