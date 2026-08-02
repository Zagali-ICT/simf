using System.Drawing;
using System.Drawing.Printing;
using QRCoder;

namespace SIMF.BadgeDesk;

/// <summary>
/// D-819 — renders and prints a badge.
///
/// <para>Native <see cref="PrintDocument"/> rather than a PDF library: the desk
/// prints one badge at a time straight to a label or card printer with no file
/// in between, and QuestPDF's Community licence is already flagged in this
/// codebase as insufficient for RSNF.</para>
///
/// <para>The QR is drawn with SQUARE modules at a whole number of pixels per
/// module. A previous SIMF badge used a rounded style and came out undecodable
/// by the scanner's <c>flutter_zxing</c>; square modules and a generous quiet
/// zone are what made it readable, so both are fixed here rather than
/// configurable.</para>
/// </summary>
public static class BadgePrinter
{
    /// <summary>Error correction level. M tolerates roughly 15% damage, which is
    /// the level a laminated badge that gets creased in a pocket needs. Higher
    /// levels would enlarge the code for a payload that is already the smallest
    /// it can be.</summary>
    private const QRCodeGenerator.ECCLevel Ecc = QRCodeGenerator.ECCLevel.M;

    /// <summary>Pixels per module in the on-screen preview. Whole number, so no
    /// module is ever rendered as a half pixel.</summary>
    private const int PreviewPixelsPerModule = 6;

    /// <summary>Renders the badge QR as a bitmap.</summary>
    public static Bitmap RenderQr(string payload, int pixelsPerModule = PreviewPixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, Ecc);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        using var stream = new MemoryStream(png);
        // Copied out of the stream: Image.FromStream keeps the stream alive for
        // the image's lifetime, and this one is disposed on the next line.
        using var decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }

    /// <summary>
    /// Prints one badge. Throws only if the printer itself fails — the caller
    /// has already stored the registration, so a printing failure is a reprint,
    /// never a lost visitor.
    /// </summary>
    public static void Print(
        PrinterSettings printerSettings,
        string payload,
        string name,
        string profileTypeName,
        long sequence)
    {
        using var qr = RenderQr(payload, pixelsPerModule: 10);
        using var document = new PrintDocument { PrinterSettings = printerSettings };
        document.DocumentName = $"SIMF badge {sequence}";
        document.PrintPage += (_, args) => DrawBadge(
            args, qr, name, profileTypeName, sequence);
        document.Print();
    }

    private static void DrawBadge(
        PrintPageEventArgs args,
        Image qr,
        string name,
        string profileTypeName,
        long sequence)
    {
        if (args.Graphics is not { } graphics) { return; }
        var bounds = args.MarginBounds;

        using var nameFont = new Font(FontFamily.GenericSansSerif, 16f, FontStyle.Bold);
        using var typeFont = new Font(FontFamily.GenericSansSerif, 11f);
        using var sequenceFont = new Font(FontFamily.GenericSansSerif, 8f);
        using var centred = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            // A name that overruns must not spill over the QR beneath it.
            Trimming = StringTrimming.EllipsisCharacter,
        };

        var nameArea = new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height * 0.18f);
        graphics.DrawString(name, nameFont, Brushes.Black, nameArea, centred);

        var typeArea = new RectangleF(
            bounds.Left, nameArea.Bottom, bounds.Width, bounds.Height * 0.10f);
        graphics.DrawString(profileTypeName, typeFont, Brushes.Black, typeArea, centred);

        // Square, and sized to the smaller edge so the code is never stretched —
        // a non-square module is the failure that made an earlier badge
        // undecodable.
        var available = Math.Min(bounds.Width, (int)(bounds.Bottom - typeArea.Bottom));
        var side = (int)(available * 0.80f);
        var qrRectangle = new Rectangle(
            bounds.Left + ((bounds.Width - side) / 2),
            (int)typeArea.Bottom + 8,
            side,
            side);
        graphics.DrawImage(qr, qrRectangle);

        // The human-readable sequence, so a badge whose QR will not scan can
        // still be reconciled by hand against the desk's file.
        var sequenceArea = new RectangleF(
            bounds.Left,
            qrRectangle.Bottom + 4,
            bounds.Width,
            Math.Max(0, bounds.Bottom - qrRectangle.Bottom - 4));
        graphics.DrawString(
            sequence.ToString("D11", System.Globalization.CultureInfo.InvariantCulture),
            sequenceFont, Brushes.Black, sequenceArea, centred);
    }
}
