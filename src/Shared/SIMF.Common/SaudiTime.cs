using System.Globalization;

namespace SIMF.Common;

/// <summary>
/// The single conversion point between the system's stored UTC instants and the
/// Saudi wall-clock the owner requires everywhere (Control Panel and app display
/// alike). All instants are persisted as UTC <see cref="DateTimeOffset"/>; every
/// user-facing render converts through here.
/// <para>
/// Saudi Arabia observes <b>AST = UTC+03:00 with no daylight saving, ever</b>, so
/// a fixed offset is exact and permanently stable. Using a fixed <see cref="Offset"/> instead
/// of <c>TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time")</c> also removes
/// the cross-platform failure mode where the Windows id is absent on a Linux host
/// (there the IANA id is <c>Asia/Riyadh</c>) — the conversion can never throw.
/// </para>
/// The app mirrors this with <c>lib/core/utils/saudi_time.dart</c>; there is no
/// shared runtime between .NET and Flutter, so the offset is duplicated by design.
/// </summary>
public static class SaudiTime
{
    /// <summary>Saudi Standard Time relative to UTC: +03:00, no DST.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    /// <summary>Default date+time render format (Saudi wall clock, 24h).</summary>
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Default date-only render format.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Projects a stored instant onto the Saudi wall clock (+03:00). The returned
    /// <see cref="DateTimeOffset"/> represents the same moment, so its
    /// <see cref="DateTimeOffset.DateTime"/> / <see cref="DateTimeOffset.Date"/>
    /// read as the local Saudi calendar day and time.
    /// </summary>
    public static DateTimeOffset ToSaudi(this DateTimeOffset instant) => instant.ToOffset(Offset);

    /// <summary>Nullable overload: null in → null out.</summary>
    public static DateTimeOffset? ToSaudi(this DateTimeOffset? instant) =>
        instant is { } value ? value.ToOffset(Offset) : null;

    /// <summary>
    /// Formats a stored instant on the Saudi wall clock using the invariant
    /// culture (stable, locale-independent digits/separators).
    /// </summary>
    public static string FormatSaudi(this DateTimeOffset instant, string format = DateTimeFormat) =>
        instant.ToOffset(Offset).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Nullable overload: returns <paramref name="fallback"/> (default empty) when
    /// the instant is null instead of throwing.
    /// </summary>
    public static string FormatSaudi(this DateTimeOffset? instant, string format = DateTimeFormat, string fallback = "") =>
        instant is { } value ? value.FormatSaudi(format) : fallback;

    /// <summary>
    /// Converts a naive Saudi wall-clock value (e.g. the text a CP admin typed into
    /// a <c>&lt;input type="datetime-local"&gt;</c> field, which carries no zone)
    /// back to the UTC instant to persist. Any Kind on the input is treated as an
    /// unspecified Saudi local time. This is the inverse of <see cref="ToSaudi(DateTimeOffset)"/>
    /// for the save path.
    /// </summary>
    public static DateTimeOffset FromSaudiWallClock(DateTime wallClock)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Offset).ToUniversalTime();
    }
}
