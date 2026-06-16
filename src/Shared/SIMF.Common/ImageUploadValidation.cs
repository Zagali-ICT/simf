namespace SIMF.Common;

/// <summary>
/// Shared image-upload content guard (D-430 follow-up) — one place for the
/// allowed image MIME types + the magic-byte signature check, replacing the
/// copies that had drifted across the avatar / ID-document / VIP-photo upload
/// paths. A value that passes <see cref="IsAllowedImage"/> is a real PNG / JPEG /
/// WebP (the declared MIME type AND the leading bytes both match), which defeats
/// a renamed-file upload (CWE-646). Callers still enforce their own size cap.
/// </summary>
public static class ImageUploadValidation
{
    /// <summary>The image MIME types accepted by every SIMF image upload.</summary>
    public static readonly IReadOnlySet<string> AllowedMimeTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

    /// <summary>True when <paramref name="contentType"/> is an allowed image type
    /// AND the bytes carry the matching magic-byte signature.</summary>
    public static bool IsAllowedImage(byte[] content, string contentType) =>
        AllowedMimeTypes.Contains(contentType)
        && MagicBytesMatch(content, contentType);

    /// <summary>Magic-byte signatures — PNG, JPEG, WebP. Case-insensitive on the
    /// MIME type so a caller need not pre-normalise.</summary>
    public static bool MagicBytesMatch(byte[] content, string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => content.Length >= 8
                && content[0] == 0x89 && content[1] == 0x50
                && content[2] == 0x4E && content[3] == 0x47
                && content[4] == 0x0D && content[5] == 0x0A
                && content[6] == 0x1A && content[7] == 0x0A,
            "image/jpeg" => content.Length >= 3
                && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/webp" => content.Length >= 12
                && content[0] == 0x52 && content[1] == 0x49
                && content[2] == 0x46 && content[3] == 0x46
                && content[8] == 0x57 && content[9] == 0x45
                && content[10] == 0x42 && content[11] == 0x50,
            _ => false,
        };
}
