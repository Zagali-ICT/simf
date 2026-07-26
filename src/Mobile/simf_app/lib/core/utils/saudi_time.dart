/// The single conversion point between the API's stored UTC instants and the
/// Saudi event-local wall clock the app renders, mirroring the backend
/// `SIMF.Common.SaudiTime`: Saudi Arabia observes AST = UTC+03:00, no daylight
/// saving ever, so a fixed offset is exact and device-timezone independent.
///
/// Never use `DateTime.toLocal()` for event times — that is the *phone's* zone,
/// so a traveller's device would show the wrong session/meeting time. Convert
/// through [saudiOf] instead.
library;

import 'package:intl/intl.dart';

/// The forum's fixed event-local offset (Riyadh, +03:00, no DST).
const Duration saudiOffset = Duration(hours: 3);

/// Projects a stored UTC instant onto the Saudi wall clock (+03:00, no DST).
/// Format the RESULT with a [DateFormat] (or read its field values): the
/// returned [DateTime]'s year/month/day/hour/minute already read as Saudi local
/// time. The input is normalised with `toUtc()` first, so the result is the
/// same instant regardless of the device's timezone.
DateTime saudiOf(DateTime instant) => instant.toUtc().add(saudiOffset);

/// "Now" on the Saudi wall clock — for today/yesterday and morning/evening
/// boundaries that must line up with the [saudiOf] labels, independent of the
/// device zone.
DateTime saudiNow() => saudiOf(DateTime.now());

/// Parses an ISO-8601 wire timestamp into a UTC [DateTime] — the ONE decoder
/// every model uses for an API instant (the contract is always UTC). [field] is
/// the wire field name, so a failure says which one broke.
///
/// BUG-011 — a missing / unparseable value used to fall back to the Unix epoch,
/// so a dropped or renamed timestamp field rendered as 1970 (03:00 AM on the
/// Saudi clock) on every row: no error, no empty state, just wrong times. It now
/// throws a [FormatException], which the API client reports as a
/// `clientMalformedResponse` failure the screen surfaces with a retry.
DateTime parseWireUtc(Object? value, String field) {
  if (value is String && value.isNotEmpty) {
    final parsed = DateTime.tryParse(value);
    if (parsed != null) {
      return parsed.toUtc();
    }
  }
  throw FormatException(
    'The wire field "$field" is not an ISO-8601 timestamp.',
    value,
  );
}

/// One 12-hour AM/PM time-of-day formatter (hoisted off the build path). No
/// explicit intl locale — like the shared date utils — so it needs no
/// `initializeDateFormatting`; it renders Latin "AM/PM", matching the backend
/// `SaudiTime.TimeFormat` and the app's existing 12h rows.
final DateFormat _saudiTime12 = DateFormat('hh:mm a');

/// Canonical 12-hour AM/PM time-of-day (zero-padded, e.g. `03:30 PM`) on the
/// Saudi wall clock. Feed a stored UTC instant.
String formatSaudiTime12(DateTime instant) =>
    _saudiTime12.format(saudiOf(instant));
