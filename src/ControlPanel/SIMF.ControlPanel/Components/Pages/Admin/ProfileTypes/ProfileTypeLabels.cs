using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

/// <summary>
/// D-125 — shared label helpers for the ProfileTypes CRUD pages and the
/// pending-approval modals. Localises the raw <c>UserType</c> string the
/// API returns ("Visitor", "Other", "Admin") via the existing
/// <c>ResUserType</c> resources so the form, the grid column, the Details
/// modal and the pending-profile View modal stay consistent.
/// </summary>
internal static class ProfileTypeLabels
{
    /// <summary>Returns the localised display name for a raw UserType string
    /// such as the one carried on <c>AdminProfileTypeSummary.UserType</c> or
    /// <c>PendingProfileResponse.UserType</c>. Falls back to the raw value
    /// when the string doesn't parse to a known enum member.</summary>
    public static string LocaliseUserType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return string.Empty; }
        return Enum.TryParse<UserType>(raw, ignoreCase: true, out var parsed)
            ? parsed.GetDisplayName()
            : raw;
    }
}
