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
    // Stored values are already Saudi wall-clock (owner decision 2026-07-31),
    // so this is now the identity. Kept as the named seam so call sites read
    // unchanged and nobody reintroduces a shift here.
    public static DateTime Local(DateTime instant) => instant;

    // 12-hour AM/PM clock time in event-local time, in the current culture.
    public static string Time(DateTime instant) =>
        Local(instant).ToString("hh:mm tt", CultureInfo.CurrentUICulture);

    // "hh:mm tt – hh:mm tt" time window in event-local time, current culture.
    public static string Window(DateTime start, DateTime end) =>
        $"{Time(start)} – {Time(end)}";

    // "dd-MM-yyyy hh:mm tt" date + 12-hour time in event-local time (e.g. a
    // notification timestamp), in the current culture.
    public static string DateTimeText(DateTime instant) =>
        Local(instant).ToString("dd-MM-yyyy hh:mm tt", CultureInfo.CurrentUICulture);
}
