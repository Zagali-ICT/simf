using System.Globalization;

namespace SIMF.Common;

/// <summary>
/// Formats the forum's event date range (the CP-editable
/// <c>OrganizationProfile.EventStartDate</c> / <c>EventEndDate</c>) as one
/// bilingual label, so every surface (Website, the seeder-generated hero label,
/// admin) renders the same config-driven string instead of a hardcoded literal.
/// Month names are explicit (never taken from the server culture) and mirror the
/// Flutter app's <c>core/utils/gregorian_month_names.dart</c>; digits are Western
/// in both languages (matching the app splash and the Figma design).
/// <para>Same-month ranges collapse to a single month + year
/// ("23-25 November 2026" / "23-25 نوفمبر 2026"); cross-month and cross-year ranges
/// spell out both endpoints ("30 November - 2 December 2026").</para>
/// </summary>
public static class EventDateRange
{
    private static readonly string[] MonthsEn =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    // Mirrors src/Mobile/simf_app/lib/core/utils/gregorian_month_names.dart so the
    // app and the server never disagree on an Arabic month name.
    private static readonly string[] MonthsAr =
    [
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر",
    ];

    /// <summary>
    /// Formats <paramref name="start"/>..<paramref name="end"/> as one label in
    /// English (<paramref name="arabic"/> = <c>false</c>) or Arabic (<c>true</c>).
    /// A reversed pair is ordered first so the earlier date always reads first, and
    /// a single-day range renders one date.
    /// </summary>
    public static string Format(DateOnly start, DateOnly end, bool arabic)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var months = arabic ? MonthsAr : MonthsEn;

        if (start == end)
        {
            return FormatFull(start, months);
        }

        // Same month and year → one month + year, day range only.
        if (start.Year == end.Year && start.Month == end.Month)
        {
            return $"{Num(start.Day)}-{Num(end.Day)} {months[start.Month - 1]} {Num(start.Year)}";
        }

        // Same year, different month → the year is shown once, at the end.
        if (start.Year == end.Year)
        {
            return $"{Num(start.Day)} {months[start.Month - 1]} - {FormatFull(end, months)}";
        }

        // Different year → both endpoints carry their own year.
        return $"{FormatFull(start, months)} - {FormatFull(end, months)}";
    }

    private static string FormatFull(DateOnly date, string[] months) =>
        $"{Num(date.Day)} {months[date.Month - 1]} {Num(date.Year)}";

    // Western digits regardless of the ambient thread culture (Arabic renders the
    // same numerals as the app splash / Figma, not Arabic-Indic).
    private static string Num(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
