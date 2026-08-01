using System.Globalization;
using SIMF.Common;

namespace SIMF.Common;

/// <summary>
/// Saudi wall-clock FORMATTING.
///
/// <para><b>Owner decision 2026-07-31 — there is no conversion left to do.</b>
/// SIMF stores every instant as a plain <see cref="DateTime"/> that is already on
/// the Saudi wall clock (see <see cref="SimfClock"/>), so a stored value means
/// exactly what it reads and the Control Panel, the Website and the app all render
/// it verbatim. This type used to be the a zoned value-to-Saudi conversion seam; converting a
/// stored value now would shift it by three hours, which is precisely the bug the
/// conversion previously existed to prevent.</para>
///
/// <para>What survives is the presentation contract: one place that decides how a
/// date, a time and a start-end window are written, so every surface agrees.</para>
/// </summary>
public static class SaudiTime
{
    /// <summary>Saudi Standard Time relative to a zoned value: +03:00, no DST. Kept for the
    /// few places that must still speak a zoned value to an EXTERNAL system (RFC 6238 TOTP
    /// counts from the Unix epoch, for instance). Nothing in SIMF's own storage or
    /// wire format uses it any more.</summary>
    public static readonly TimeSpan Offset = SimfClock.Offset;

    /// <summary>Default date+time render format (12-hour AM/PM).</summary>
    public const string DateTimeFormat = "dd-MM-yyyy hh:mm tt";

    /// <summary>Default date-only render format.</summary>
    public const string DateFormat = "dd-MM-yyyy";

    /// <summary>Time-only render format (12-hour AM/PM).</summary>
    public const string TimeFormat = "hh:mm tt";

    /// <summary>
    /// Formats a stored value using the invariant culture (stable,
    /// locale-independent digits and separators). No conversion — the value is
    /// already Saudi local.
    /// </summary>
    public static string FormatSaudi(this DateTime value, string format = DateTimeFormat) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Nullable overload: returns <paramref name="fallback"/> (default empty) when
    /// the value is null instead of throwing.
    /// </summary>
    public static string FormatSaudi(
        this DateTime? value, string format = DateTimeFormat, string fallback = "") =>
        value is { } present ? present.FormatSaudi(format) : fallback;

    /// <summary>
    /// Renders the time-of-day (12-hour AM/PM). Defaults to the invariant culture
    /// (Latin digits + "AM"/"PM", matching the Control Panel); pass the current UI
    /// culture on the bilingual Website so Arabic renders its localized digits and
    /// meridiem markers.
    /// </summary>
    public static string FormatSaudiTime(this DateTime value, CultureInfo? culture = null) =>
        value.ToString(TimeFormat, culture ?? CultureInfo.InvariantCulture);

    /// <summary>
    /// Renders a "start – end" time window (12-hour AM/PM); the en-dash separator
    /// matches the public agenda. See <see cref="FormatSaudiTime"/> for the culture
    /// rule.
    /// </summary>
    public static string FormatSaudiWindow(
        DateTime start, DateTime end, CultureInfo? culture = null) =>
        $"{start.FormatSaudiTime(culture)} – {end.FormatSaudiTime(culture)}";

    /// <summary>
    /// Normalises a value typed into a <c>&lt;input type="datetime-local"&gt;</c>
    /// field. That text carries no zone and is already Saudi wall-clock, so this is
    /// now only a <see cref="DateTimeKind"/> normalisation and deliberately does NOT
    /// shift the value. Kept as the named save-path seam so call sites read as
    /// before and nobody reintroduces a conversion here.
    /// </summary>
    public static DateTime FromSaudiWallClock(DateTime wallClock) =>
        DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
}
