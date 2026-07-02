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
            _qrSvg = BuildQrSvg(Response.QrId);
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

    private static string BuildQrSvg(string qrId)
    {
        using var generator = new QRCoder.QRCodeGenerator();
        using var data = generator.CreateQrCode(qrId, QRCoder.QRCodeGenerator.ECCLevel.Q);
        return new QRCoder.SvgQRCode(data).GetGraphic(
            pixelsPerModule: 6,
            darkColorHex: "#0B2545",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true);
    }
}
