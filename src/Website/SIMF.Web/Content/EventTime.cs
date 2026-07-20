using System.Globalization;

namespace SIMF.Web.Content;

// Shared event-time formatting for the public "ln-" pages (Programme agenda,
// Session Detail, …). The forum runs on Riyadh time (+03:00); every public page
// buckets days and renders session times in that fixed event-local offset, so
// the agenda is consistent regardless of the server's timezone. One source of
// truth so the pages never drift apart (they each used to carry their own copy
// of the offset and the "HH:mm – HH:mm" formatter).
public static class EventTime
{
    // The forum's fixed event-local offset (Riyadh, +03:00).
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    // The instant expressed in event-local time - used for day grouping + labels.
    public static DateTimeOffset Local(DateTimeOffset instant) => instant.ToOffset(Offset);

    // "HH:mm – HH:mm" time window in event-local time, in the current culture.
    public static string Window(DateTimeOffset start, DateTimeOffset end) =>
        $"{Local(start).ToString("HH:mm", CultureInfo.CurrentUICulture)} – " +
        $"{Local(end).ToString("HH:mm", CultureInfo.CurrentUICulture)}";
}
