namespace SIMF.Common;

/// <summary>
/// The 13 official Saudi administrative regions, used for the
/// birth-location dropdown (FR #4): a Saudi registrant picks a region; everyone
/// else types their place of birth free-form "as in passport". A fixed constant
/// (the regions are official and stable), mirrored by the app's
/// <c>saudi_regions.dart</c> — there is no shared runtime between .NET and
/// Flutter. The selected region's localized <b>name</b> is stored in the existing
/// free-text <c>UserProfile.PlaceOfBirth</c> column, so this adds no schema and
/// no new validation — it only constrains the input for Saudis.
/// </summary>
public static class SaudiRegions
{
    /// <summary>One region: a stable <paramref name="Code"/> plus its Arabic and
    /// English display names.</summary>
    public sealed record Region(string Code, string Arabic, string English);

    /// <summary>The 13 regions, in the conventional order.</summary>
    public static readonly IReadOnlyList<Region> All =
    [
        new("riyadh", "منطقة الرياض", "Riyadh"),
        new("makkah", "منطقة مكة المكرمة", "Makkah"),
        new("madinah", "منطقة المدينة المنورة", "Al Madinah"),
        new("eastern", "المنطقة الشرقية", "Eastern Province"),
        new("asir", "منطقة عسير", "Asir"),
        new("tabuk", "منطقة تبوك", "Tabuk"),
        new("hail", "منطقة حائل", "Hail"),
        new("northern", "منطقة الحدود الشمالية", "Northern Borders"),
        new("jazan", "منطقة جازان", "Jazan"),
        new("najran", "منطقة نجران", "Najran"),
        new("bahah", "منطقة الباحة", "Al Bahah"),
        new("jawf", "منطقة الجوف", "Al Jawf"),
        new("qassim", "منطقة القصيم", "Al Qassim"),
    ];

    /// <summary>The region whose <see cref="Region.Code"/> equals <paramref name="code"/>, or null.</summary>
    public static Region? ByCode(string? code) =>
        All.FirstOrDefault(r => r.Code == code);

    /// <summary>
    /// The region whose Arabic or English name equals <paramref name="name"/>
    /// (trimmed), or null — maps a stored place-of-birth string (saved in either
    /// language) back to a region so the dropdown survives a UI-culture switch.
    /// </summary>
    public static Region? ByName(string? name)
    {
        var n = name?.Trim() ?? string.Empty;
        return n.Length == 0
            ? null
            : All.FirstOrDefault(r => r.Arabic == n || r.English == n);
    }
}
