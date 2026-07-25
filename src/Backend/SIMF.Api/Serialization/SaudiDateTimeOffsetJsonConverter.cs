using System.Text.Json;
using System.Text.Json.Serialization;
using SIMF.Common;

namespace SIMF.Api.Serialization;

/// <summary>
/// Serializes every API <see cref="DateTimeOffset"/> in Saudi local time
/// (+03:00, no daylight saving) so the wire carries no UTC "Z" and no
/// user-facing UTC — the app, Control Panel and integrators all read the Saudi
/// wall clock. Only the offset representation changes; the underlying instant is
/// preserved, and reads accept any ISO-8601 offset and keep the exact instant,
/// so storage (instant-based) is unaffected.
/// </summary>
public sealed class SaudiDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(
        Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToOffset(SaudiTime.Offset));
}
