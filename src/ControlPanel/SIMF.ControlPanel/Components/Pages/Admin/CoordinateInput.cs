using System.Globalization;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>
/// Reads the latitude/longitude text pair shared by the five contact-block forms
/// (speakers, sponsors, booths, exhibitors, media partners), which all carried a
/// byte-identical copy of this parse.
/// </summary>
/// <remarks>
/// The pair is all-or-nothing: both blank means "no coordinates", exactly one
/// blank is a mistake worth telling the admin about. Range checking is NOT done
/// here — the service owns the real-world bounds and answers with a bilingual 400.
/// </remarks>
internal static class CoordinateInput
{
    /// <summary>Shown when only one of the two fields was filled in.</summary>
    internal const string IncompleteMessageKey = "Admin.ContactField.LatLongHint";

    /// <summary>Shown when a filled-in field is not a number.</summary>
    internal const string UnparsableMessageKey = "Admin.ContactField.LatLongInvalid";

    /// <summary>
    /// Parses the pair. Returns null when the input is acceptable (including the
    /// both-blank case, which yields two nulls), otherwise the resource key of the
    /// message to show. Localisation stays with the calling page.
    /// </summary>
    internal static string? Read(
        string? latitudeText,
        string? longitudeText,
        out double? latitude,
        out double? longitude)
    {
        latitude = null;
        longitude = null;

        var hasLatitude = !string.IsNullOrWhiteSpace(latitudeText);
        var hasLongitude = !string.IsNullOrWhiteSpace(longitudeText);
        if (hasLatitude != hasLongitude)
        {
            return IncompleteMessageKey;
        }
        if (!hasLatitude)
        {
            return null;
        }

        if (!double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLatitude)
            || !double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLongitude))
        {
            return UnparsableMessageKey;
        }

        latitude = parsedLatitude;
        longitude = parsedLongitude;
        return null;
    }
}
