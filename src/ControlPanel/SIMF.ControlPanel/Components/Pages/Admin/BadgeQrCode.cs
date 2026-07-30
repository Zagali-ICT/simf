namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>
/// Renders the badge QR as an inline SVG. The walk-in success modal and the
/// print-bag sheet carried a byte-identical copy of this, colours included.
/// </summary>
/// <remarks>
/// The colours are deliberately NOT theme tokens: this is a scannable code, not
/// chrome. It must stay high-contrast and identical on screen and on paper
/// regardless of the operator's light/dark preference, and a themed value would
/// silently break scanning in dark mode.
/// </remarks>
internal static class BadgeQrCode
{
    /// <summary>SIMF navy — the QR's dark modules.</summary>
    private const string DarkColorHex = "#0B2545";

    /// <summary>Pure white — the QR's quiet zone and light modules.</summary>
    private const string LightColorHex = "#FFFFFF";

    /// <summary>Six device pixels per module: small enough for the badge, large
    /// enough that a phone camera resolves it off a printed sheet.</summary>
    private const int PixelsPerModule = 6;

    internal static string ToSvg(string qrId)
    {
        using var generator = new QRCoder.QRCodeGenerator();
        using var data = generator.CreateQrCode(qrId, QRCoder.QRCodeGenerator.ECCLevel.Q);
        return new QRCoder.SvgQRCode(data).GetGraphic(
            PixelsPerModule,
            darkColorHex: DarkColorHex,
            lightColorHex: LightColorHex,
            drawQuietZones: true);
    }
}
