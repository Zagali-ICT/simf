using System.Globalization;

namespace SIMF.Common;

/// <summary>
/// Helpers for picking the right-language message from an <see cref="ApiError"/>
/// or <see cref="ApiErrorDetail"/> based on the current UI culture (D-030).
/// </summary>
public static class ApiErrorExtensions
{
    /// <summary>
    /// Returns the message for <see cref="CultureInfo.CurrentUICulture"/> — Arabic
    /// when the culture is <c>ar</c> (or any <c>ar-XX</c>), English otherwise.
    /// </summary>
    public static string MessageForCurrentCulture(this ApiError error) =>
        IsArabic(CultureInfo.CurrentUICulture) ? error.MessageArabic : error.Message;

    /// <summary>
    /// Returns the message for <see cref="CultureInfo.CurrentUICulture"/> — Arabic
    /// when the culture is <c>ar</c> (or any <c>ar-XX</c>), English otherwise.
    /// </summary>
    public static string MessageForCurrentCulture(this ApiErrorDetail detail) =>
        IsArabic(CultureInfo.CurrentUICulture) ? detail.MessageArabic : detail.Message;

    /// <summary>
    /// Returns the English or Arabic text for <see cref="CultureInfo.CurrentUICulture"/>.
    /// Use it for hard-coded fallback strings, the equivalent of an
    /// <c>ApiError</c> for an inline message (D-030).
    /// </summary>
    public static string Bilingual(string english, string arabic) =>
        IsArabic(CultureInfo.CurrentUICulture) ? arabic : english;

    private static bool IsArabic(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);
}
