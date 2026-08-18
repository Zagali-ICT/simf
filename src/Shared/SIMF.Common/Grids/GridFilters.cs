// Tests: SIMF.Api.Tests/GridColumnsTests.cs
using System.Globalization;

namespace SIMF.Common.Grids;

/// <summary>
/// The bilingual failures and the date parsing shared by the inferred column
/// filters and by any hand-written <c>AddFilter</c> builder, so the two cannot
/// drift apart in their error shape or their idea of a calendar day.
/// </summary>
public static class GridFilters
{
    /// <summary>The longest raw filter value echoed back in an error. The value is
    /// unbounded client text and it is echoed four times (English and Arabic, in
    /// the message and in the detail) straight into a Control Panel toast, so a
    /// 200 KB filter value would otherwise become a near-megabyte error body.</summary>
    private const int MaxEchoLength = 64;

    /// <summary>The standard 400 for a filter value that will not parse. A value
    /// that will not parse is never a skipped predicate: skipping widens the
    /// result set, and on an admin grid over people a silently widened set is a
    /// disclosure, not an inconvenience.</summary>
    /// <param name="key">The filter column key, named in the message.</param>
    /// <param name="raw">The offending value; truncated before it is echoed.</param>
    /// <param name="expected">What the column accepts, e.g. "a whole number".</param>
    public static ApiException ValueInvalid(string key, string raw, string? expected = null)
    {
        var echo = Echo(raw);
        var suffix = expected is null ? string.Empty : $" Expected {expected}.";
        var english = $"'{echo}' is not a valid value for the '{key}' filter.{suffix}";
        var arabic = $"القيمة '{echo}' غير صالحة لتصفية العمود '{key}'.";

        return new ApiException(
            ErrorCodes.GridFilterValueInvalid, 400, english, arabic,
            [new ApiErrorDetail { Field = key, Message = english, MessageArabic = arabic }]);
    }

    /// <summary>
    /// Parses a filter value into the Saudi calendar day it names, at midnight,
    /// with <see cref="DateTimeKind.Unspecified"/>.
    ///
    /// <para>
    /// SIMF stores Saudi wall-clock time, not UTC — see <see cref="SimfClock"/>:
    /// a stored value means exactly what it reads, Kind deliberately left
    /// unspecified. So a day filter needs NO zone adjustment, and applying one
    /// would be a bug: parsing "2026-08-16T00:00+03:00" as UTC shifts it to
    /// 2026-08-15T21:00 and filters the previous day.
    /// </para>
    ///
    /// <para>
    /// A value that carries an explicit offset is re-expressed at +03:00, which
    /// preserves the instant and yields the Saudi reading the column stores. A
    /// value with no offset is taken as already being that reading.
    /// </para>
    /// </summary>
    public static DateTime ParseDay(string key, string raw)
    {
        if (!DateTime.TryParse(
                raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw ValueInvalid(key, raw, "a date such as 2026-08-16");
        }

        // TryParse returns Kind.Local when the text carried an offset (or a "Z")
        // and .NET converted it to the host zone; Kind.Unspecified when it did not.
        var saudi = parsed.Kind == DateTimeKind.Unspecified
            ? parsed
            : new DateTimeOffset(parsed).ToOffset(SimfClock.Offset).DateTime;

        return DateTime.SpecifyKind(saudi.Date, DateTimeKind.Unspecified);
    }

    /// <summary>The same calendar day as a <see cref="DateOnly"/>, for the
    /// date-only columns in the domain.</summary>
    public static DateOnly ParseDate(string key, string raw) =>
        DateOnly.FromDateTime(ParseDay(key, raw));

    private static string Echo(string raw) =>
        raw.Length <= MaxEchoLength ? raw : raw[..MaxEchoLength] + "...";
}
