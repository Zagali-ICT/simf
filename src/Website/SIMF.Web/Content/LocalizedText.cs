using System.Globalization;

namespace SIMF.Web.Content;

// Shared bilingual helpers for the public "ln-" pages (Speakers, SessionDetail, …).
// One source of truth for the "Arabic-preferred in RTL, English-preferred in LTR,
// fall back to the other language" selection so the pages never drift apart (they
// each used to carry their own copy with subtly different fallback semantics).
public static class LocalizedText
{
    public static bool Rtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    /// <summary>Arabic-preferred in RTL, English-preferred in LTR; falls back to
    /// the other language when the preferred one is blank.</summary>
    public static string Pick(string en, string ar) =>
        Rtl ? (string.IsNullOrWhiteSpace(ar) ? en : ar)
            : (string.IsNullOrWhiteSpace(en) ? ar : en);

    /// <summary>Nullable <see cref="Pick"/>: returns null when the chosen value is
    /// blank (so a caller can omit an optional field).</summary>
    public static string? PickOrNull(string? en, string? ar)
    {
        var value = Rtl
            ? (string.IsNullOrWhiteSpace(ar) ? en : ar)
            : (string.IsNullOrWhiteSpace(en) ? ar : en);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

// The same-origin speaker-photo URL the public pages render: the StoredFile
// SpeakerPhoto asset proxy when the speaker has one, else the legacy relative
// path, else empty (the card shows its gradient backdrop). Shared by the
// Speakers list and the Session-detail speakers grid.
public static class SpeakerPhoto
{
    public static string Url(Guid speakerId, bool hasPhotoAsset, string? photoRelativePath) =>
        hasPhotoAsset ? $"/content/assets/SpeakerPhoto/{speakerId}/image"
        : !string.IsNullOrWhiteSpace(photoRelativePath) ? photoRelativePath!
        : string.Empty;
}
