/// The int-backed wire enums a session carries: its broadcast [SessionStatus],
/// a speaker's [SessionSpeakerRole], and the [SessionType] the programme tabs
/// filter on. Each mirrors a frozen `SIMF.Common.Enums` value and decodes
/// tolerantly (int OR name) per the append-only wire rule (D-219).
library;

/// The session broadcast lifecycle — mirrors `SIMF.Common.Enums.SessionStatus`
/// (frozen, int-backed: Scheduled=0, Held=1, Recorded=2, Published=3). The wire
/// value is an **int** — there is no string-enum converter anywhere in the API
/// (verified D-299), so the JSON carries `0..3`, not the name. [fromJson]
/// decodes tolerantly (int OR name; unknown → [scheduled]) per the append-only
/// wire rule (D-219).
enum SessionStatus {
  scheduled(0, 'Scheduled'),
  held(1, 'Held'),
  recorded(2, 'Recorded'),
  published(3, 'Published');

  const SessionStatus(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static SessionStatus fromJson(Object? value) {
    if (value is String) {
      for (final status in values) {
        if (status.wireName == value) {
          return status;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final status in values) {
        if (status.wireValue == asInt) {
          return status;
        }
      }
    }
    return SessionStatus.scheduled;
  }
}

/// The role a speaker plays in a session — mirrors
/// `SIMF.Common.Enums.SessionSpeakerRole` (frozen, int-backed: Speaker=0,
/// Host=1). Int on the wire; [fromJson] tolerant (int OR name; unknown →
/// [speaker]).
enum SessionSpeakerRole {
  speaker(0, 'Speaker'),
  host(1, 'Host');

  const SessionSpeakerRole(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static SessionSpeakerRole fromJson(Object? value) {
    if (value is String) {
      for (final role in values) {
        if (role.wireName == value) {
          return role;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final role in values) {
        if (role.wireValue == asInt) {
          return role;
        }
      }
    }
    return SessionSpeakerRole.speaker;
  }
}

/// D-452 (Figma 883:2308 type tabs) — the kind of a session, driving the app's
/// "جلسات / ورش العمل" tabs (the احداث tab was dropped for the 3-tab frame,
/// D-598 — an event-typed session shows only under الكل). Int on the wire
/// (mirrors `SIMF.Common.Enums.SessionType`; the enum itself is frozen D-219);
/// [fromJson] is tolerant (int OR name); a null / absent / unknown value
/// decodes to null (an untyped session shows only under the "الكل / All" tab).
enum SessionType {
  workshop(0, 'Workshop'),
  session(1, 'Session'),
  event(2, 'Event');

  const SessionType(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static SessionType? fromJson(Object? value) {
    if (value is String) {
      for (final t in values) {
        if (t.wireName == value) {
          return t;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final t in values) {
        if (t.wireValue == asInt) {
          return t;
        }
      }
    }
    return null;
  }
}
