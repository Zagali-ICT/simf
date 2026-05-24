namespace SIMF.Common;

/// <summary>
/// ISO 3166-1 alpha-2 country list used by the user-profile nationality
/// picker (decisions D-046 b, P8 — D-049; renamed). Carries the code,
/// the English name and the Arabic name; the user's nationality
/// persists as the 2-letter code in <c>UserProfile.NationalityCode</c>.
///
/// <para>This is a curated set of ~60 entries covering Saudi + GCC + the
/// nationalities most likely to attend SIMF. The full ISO 3166-1 list
/// (~250 entries) is its own increment when an admin requests an
/// expansion; until then, an unmatched code is rejected by the validator
/// and the user must pick from this set.</para>
/// </summary>
public static class Countries
{
    /// <summary>One country entry.</summary>
    public sealed record Country(string Code, string NameEn, string NameAr);

    private static readonly Country[] _all =
    {
        // GCC + neighbours first — the expected majority.
        new("SA", "Saudi Arabia", "المملكة العربية السعودية"),
        new("AE", "United Arab Emirates", "الإمارات العربية المتحدة"),
        new("BH", "Bahrain", "البحرين"),
        new("KW", "Kuwait", "الكويت"),
        new("OM", "Oman", "عُمان"),
        new("QA", "Qatar", "قطر"),
        new("YE", "Yemen", "اليمن"),
        new("IQ", "Iraq", "العراق"),
        new("JO", "Jordan", "الأردن"),
        new("SY", "Syria", "سوريا"),
        new("LB", "Lebanon", "لبنان"),
        new("PS", "Palestine", "فلسطين"),
        new("EG", "Egypt", "مصر"),
        new("SD", "Sudan", "السودان"),
        new("LY", "Libya", "ليبيا"),
        new("TN", "Tunisia", "تونس"),
        new("DZ", "Algeria", "الجزائر"),
        new("MA", "Morocco", "المغرب"),
        new("MR", "Mauritania", "موريتانيا"),
        new("SO", "Somalia", "الصومال"),
        new("DJ", "Djibouti", "جيبوتي"),
        new("KM", "Comoros", "جزر القمر"),

        // Major-attendance nationalities.
        new("TR", "Türkiye", "تركيا"),
        new("IR", "Iran", "إيران"),
        new("PK", "Pakistan", "باكستان"),
        new("IN", "India", "الهند"),
        new("BD", "Bangladesh", "بنغلاديش"),
        new("LK", "Sri Lanka", "سريلانكا"),
        new("ID", "Indonesia", "إندونيسيا"),
        new("MY", "Malaysia", "ماليزيا"),
        new("PH", "Philippines", "الفلبين"),
        new("CN", "China", "الصين"),
        new("JP", "Japan", "اليابان"),
        new("KR", "South Korea", "كوريا الجنوبية"),
        new("SG", "Singapore", "سنغافورة"),
        new("TH", "Thailand", "تايلاند"),
        new("VN", "Vietnam", "فيتنام"),

        // Europe — common shipbuilders / maritime industry presence.
        new("GB", "United Kingdom", "المملكة المتحدة"),
        new("FR", "France", "فرنسا"),
        new("DE", "Germany", "ألمانيا"),
        new("IT", "Italy", "إيطاليا"),
        new("ES", "Spain", "إسبانيا"),
        new("PT", "Portugal", "البرتغال"),
        new("NL", "Netherlands", "هولندا"),
        new("BE", "Belgium", "بلجيكا"),
        new("CH", "Switzerland", "سويسرا"),
        new("SE", "Sweden", "السويد"),
        new("NO", "Norway", "النرويج"),
        new("DK", "Denmark", "الدنمارك"),
        new("FI", "Finland", "فنلندا"),
        new("PL", "Poland", "بولندا"),
        new("GR", "Greece", "اليونان"),
        new("RU", "Russia", "روسيا"),
        new("UA", "Ukraine", "أوكرانيا"),

        // Americas.
        new("US", "United States", "الولايات المتحدة"),
        new("CA", "Canada", "كندا"),
        new("MX", "Mexico", "المكسيك"),
        new("BR", "Brazil", "البرازيل"),
        new("AR", "Argentina", "الأرجنتين"),

        // Africa.
        new("ZA", "South Africa", "جنوب أفريقيا"),
        new("NG", "Nigeria", "نيجيريا"),
        new("KE", "Kenya", "كينيا"),
        new("ET", "Ethiopia", "إثيوبيا"),

        // Oceania.
        new("AU", "Australia", "أستراليا"),
        new("NZ", "New Zealand", "نيوزيلندا"),
    };

    /// <summary>Every supported country, in the natural display order.</summary>
    public static IReadOnlyList<Country> All => _all;

    /// <summary>The lookup index by ISO code (uppercased).</summary>
    private static readonly Dictionary<string, Country> ByCode =
        _all.ToDictionary(country => country.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the supplied code is one of the supported countries.</summary>
    public static bool IsKnown(string? code) =>
        !string.IsNullOrEmpty(code) && ByCode.ContainsKey(code);

    /// <summary>Returns the country for the code, or null when unknown.</summary>
    public static Country? Find(string? code) =>
        code is null ? null : ByCode.TryGetValue(code, out var country) ? country : null;
}
