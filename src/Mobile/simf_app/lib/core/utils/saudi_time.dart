/// Saudi wall-clock time for the app.
///
/// **Owner decision 2026-07-31 — there is no conversion left to do.** The API
/// stores and sends Saudi local wall-clock time as a zone-free ISO-8601 string
/// (`2026-11-23T09:00:00`, no `Z`, no `+03:00`), mirroring the backend
/// `SIMF.Common.SimfClock`. A value means exactly what it reads, on every screen.
///
/// This library used to be the UTC-to-Saudi conversion point. Converting now
/// would shift every timestamp by three hours, so [saudiOf] is the identity and
/// the wire decoder deliberately does **not** call `toUtc()`.
///
/// Still true, and still why this file exists rather than raw `DateTime.parse`:
/// never call `toLocal()` or `toUtc()` on an event time. Those are the *phone's*
/// zone, so a traveller's device would shift the session times it shows. Event
/// times are Riyadh times for everyone.
library;

import 'package:intl/intl.dart';

/// The forum's fixed event-local offset (Riyadh, +03:00, no DST). Needed now
/// only to derive "now" from the device clock — never for a stored value.
const Duration saudiOffset = Duration(hours: 3);

/// Identity. A stored value is already on the Saudi wall clock, so there is
/// nothing to project. Kept as the named seam so the existing call sites read
/// unchanged and nobody reintroduces a shift here.
DateTime saudiOf(DateTime stored) => stored;

/// "Now" on the Saudi wall clock. This one DOES convert, and must: it starts
/// from the device clock, which may be in any timezone, so it is normalised
/// through UTC and offset to Riyadh. That keeps "is this session live yet"
/// comparisons lined up with stored values wherever the phone is.
DateTime saudiNow() => DateTime.now().toUtc().add(saudiOffset);

/// Parses an ISO-8601 wire timestamp — the ONE decoder every model uses for an
/// API timestamp. [field] is the wire field name, so a failure says which one
/// broke.
///
/// Two wire shapes are handled, and the distinction matters:
///
/// * **Zone-free** (`2026-11-23T09:00:00`) — what the API sends now. Dart parses
///   it to a non-UTC [DateTime] whose fields are exactly the string's, so it is
///   taken verbatim. `toUtc()` is deliberately NOT called: it would apply the
///   DEVICE's offset and shift the value on any phone outside Riyadh.
/// * **Zone-bearing** (`...Z` or `...+03:00`) — a legacy server. Dart normalises
///   both to UTC while parsing, discarding the original offset, so the wall-clock
///   reading cannot be recovered from the fields alone. Legacy SIMF stored UTC
///   and displayed +03:00, so the Saudi offset is re-applied — which lands
///   `09:00Z` on 12:00 and `09:00+03:00` back on 09:00, both correct.
///
/// BUG-011 — a missing / unparseable value used to fall back to the Unix epoch,
/// so a dropped or renamed timestamp field rendered as 1970 on every row: no
/// error, no empty state, just wrong times. It now throws a [FormatException],
/// which the API client reports as a `clientMalformedResponse` failure the
/// screen surfaces with a retry.
DateTime parseWireDateTime(Object? value, String field) {
  if (value is String && value.isNotEmpty) {
    final parsed = DateTime.tryParse(value);
    if (parsed != null) {
      return parsed.isUtc
          ? _stripZone(parsed.add(saudiOffset))
          : _stripZone(parsed);
    }
  }
  throw FormatException(
    'The wire field "$field" is not an ISO-8601 timestamp.',
    value,
  );
}

/// Nullable sibling of [parseWireDateTime] for optional wire timestamps:
/// null / empty / unparseable yields null instead of throwing.
///
/// Every model must decode through this or [parseWireDateTime] rather than
/// calling `DateTime.tryParse` directly. A raw parse of a legacy `...Z` value
/// returns a zone-bearing [DateTime] whose fields read three hours behind the
/// Saudi clock, and nothing downstream corrects it — the reading is simply
/// wrong, quietly.
DateTime? parseWireOrNull(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = DateTime.tryParse(value);
    if (parsed != null) {
      return parsed.isUtc
          ? _stripZone(parsed.add(saudiOffset))
          : _stripZone(parsed);
    }
  }
  return null;
}

/// Encodes a value for the wire: zone-free ISO-8601, matching what the API sends
/// and expects. Never `toUtc()` — that would transmit a different clock time
/// than the user picked.
String formatWire(DateTime value) => _stripZone(value).toIso8601String();

/// Drops any zone marker while keeping the wall-clock reading.
DateTime _stripZone(DateTime value) => DateTime(
      value.year,
      value.month,
      value.day,
      value.hour,
      value.minute,
      value.second,
      value.millisecond,
      value.microsecond,
    );

/// One 12-hour AM/PM time-of-day formatter (hoisted off the build path). No
/// explicit intl locale — like the shared date utils — so it needs no
/// `initializeDateFormatting`; it renders Latin "AM/PM", matching the backend
/// `SaudiTime.TimeFormat` and the app's existing 12h rows.
final DateFormat _saudiTime12 = DateFormat('hh:mm a');

/// Canonical 12-hour AM/PM time-of-day (zero-padded, e.g. `03:30 PM`) on the
/// Saudi wall clock.
String formatSaudiTime12(DateTime instant) => _saudiTime12.format(instant);
