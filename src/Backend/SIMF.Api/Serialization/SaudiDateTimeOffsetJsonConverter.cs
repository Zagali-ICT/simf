using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIMF.Api.Serialization;

/// <summary>
/// The wire format for every API <see cref="DateTime"/>.
///
/// <para>Owner decision 2026-07-31 — SIMF carries Saudi local wall-clock time and
/// <b>nothing zoned anywhere</b>. Values are written as naive ISO-8601 with <b>no zone
/// suffix and no offset</b> (<c>2026-11-23T09:00:00</c>), because a stored value
/// already IS the Saudi wall clock: appending <c>Z</c> would claim it is zoned and
/// every client would then shift it by three hours, and appending <c>+03:00</c>
/// would invite clients to re-project it into their own device timezone. A bare
/// local timestamp is the only form that survives both.</para>
///
/// <para>Reads are deliberately tolerant: a client that still sends <c>Z</c> or an
/// explicit offset is accepted, and the zone information is DISCARDED rather than
/// applied — the sender's wall-clock reading is what SIMF means. Discarding is the
/// right call here precisely because the alternative (converting) is the silent
/// three-hour bug this whole change exists to remove.</para>
/// </summary>
public sealed class SaudiDateTimeOffsetJsonConverter : JsonConverter<DateTime>
{
    /// <summary>Round-trippable, zone-free, sortable. Fixed format so the wire is
    /// stable regardless of the server's culture.</summary>
    private const string WireFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

    public override DateTime Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();

        // Take the wall-clock reading the caller wrote and drop any zone tag, so
        // nothing downstream can re-interpret it. This used to branch on the Kind
        // and call ToUniversalTime() on one side — a no-op, since that side was
        // already flagged as such, so both branches produced exactly this.
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    public override void Write(
        Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(WireFormat, CultureInfo.InvariantCulture));
}
