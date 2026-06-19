/// The mobile app's role enum. Drives what the user can see and do
/// inside the Flutter UI.
///
/// Confirmed values (2026-05-29, DECISIONS_LOG D-004):
///
/// | Wire name | Wire int | Meaning |
/// |-----------|----------|---------|
/// | `Guest`     | 0 | Not registered — anonymous app user. |
/// | `Visitor`   | 1 | Any registered attendee. |
/// | `Moderator` | 2 | محاور — handles live Q&A and audience interaction. |
/// | `Staff`     | 3 | Venue staff with check-in / check-out responsibilities. |
///
/// This enum is **not** the same as the Control Panel's `UserType`. The two
/// serve different surfaces. The mobile app's role precedence and the
/// role-versus-screen access map are confirmed in SIMF-RPM-001 (gate D1,
/// SIMF-MAA-001 OI-3).
enum AppRole {
  guest(0, 'Guest'),
  visitor(1, 'Visitor'),
  moderator(2, 'Moderator'),
  staff(3, 'Staff');

  const AppRole(this.wireValue, this.wireName);

  /// The integer used on the wire when an int representation is needed.
  final int wireValue;

  /// The name used on the wire (matches the backend enum case).
  final String wireName;

  /// Parses an [AppRole] from a JSON value. Accepts either the wire name
  /// (`"Visitor"`) or the wire int (`1`). Falls back to [AppRole.guest]
  /// when the value is null or unrecognised — safer than throwing on every
  /// unknown role, since a new backend role (Staff added later, etc.) would
  /// otherwise crash the app.
  static AppRole fromJson(Object? value) {
    if (value is String) {
      for (final role in AppRole.values) {
        if (role.wireName == value) {
          return role;
        }
      }
    } else if (value is int) {
      for (final role in AppRole.values) {
        if (role.wireValue == value) {
          return role;
        }
      }
    }
    return AppRole.guest;
  }

  /// True when this role is at least the supplied role in the precedence
  /// `guest < visitor < moderator < staff`. Use for screen-level gates.
  bool isAtLeast(AppRole minimum) => wireValue >= minimum.wireValue;
}
