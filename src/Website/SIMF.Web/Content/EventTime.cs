using System.Globalization;

namespace SIMF.Web.Content;

// Shared event-time formatting for the public "ln-" pages (Programme agenda,
// Session Detail, …). The forum runs on Riyadh time (+03:00); every public page
// buckets days and renders session times in that fixed event-local offset, so
// the agenda is consistent regardless of the server's timezone. One source of
// truth so the pages never drift apart (they each used to carry their own copy
// of the offset and the 12-hour formatter).
public static class EventTime
{
    // The forum's fixed event-local offset (Riyadh, +03:00).
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    // The instant expressed in event-local time - used for day grouping + labels.
    public static DateTimeOffset Local(DateTimeOffset instant) => instant.ToOffset(Offset);

    // 12-hour AM/PM clock time in event-local time, in the current culture.
    public static string Time(DateTimeOffset instant) =>
        Local(instant).ToString("hh:mm tt", CultureInfo.CurrentUICulture);

    // "hh:mm tt – hh:mm tt" time window in event-local time, current culture.
    public static string Window(DateTimeOffset start, DateTimeOffset end) =>
        $"{Time(start)} – {Time(end)}";

    // "dd-MM-yyyy hh:mm tt" date + 12-hour time in event-local time (e.g. a
    // notification timestamp), in the current culture.
    public static string DateTimeText(DateTimeOffset instant) =>
        Local(instant).ToString("dd-MM-yyyy hh:mm tt", CultureInfo.CurrentUICulture);
}
