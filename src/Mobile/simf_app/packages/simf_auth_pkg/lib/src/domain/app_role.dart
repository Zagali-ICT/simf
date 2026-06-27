/// The mobile app's role enum. Drives what the user can see and do
/// inside the Flutter UI.
///
/// Confirmed values (2026-05-29, DECISIONS_LOG D-004; D-519 added Exhibitor):
///
/// | Wire name   | Wire int | Meaning |
/// |-------------|----------|---------|
/// | `Guest`     | 0 | Not registered — anonymous app user (also a signed-in but unapproved account). |
/// | `Visitor`   | 1 | Any registered attendee. |
/// | `Moderator` | 2 | محاور — runs a session's live Q&A desk. |
/// | `Staff`     | 3 | Venue gate staff (badge scan + walk-in registration). |
/// | `Exhibitor` | 4 | العارض — a Visitor **plus** scan-visitor-QR + add-to-contacts. |
///
/// This enum is **not** the same as the Control Panel's `UserType`. The two
/// serve different surfaces.
///
/// **NOTE (D-519): the integer is NOT a capability ladder.** Exhibitor (4) is
/// "Visitor + extras", not "above Staff". Screen access is governed by an
/// explicit per-route allowed-roles set in `app/router.dart` (`_routeRoles`),
/// not by [isAtLeast]. [isAtLeast] remains only for the legacy linear
/// `guest < visitor < moderator < staff` comparisons that predate the set model.
enum AppRole {
  guest(0, 'Guest'),
  visitor(1, 'Visitor'),
  moderator(2, 'Moderator'),
  staff(3, 'Staff'),
  exhibitor(4, 'Exhibitor');

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
  ///
  /// **The live wire is the NAME, never the int** (`/app/users/me.appRole` and
  /// the `mobile_app_role` JWT claim both carry the string). The int branch is a
  /// defensive fallback only: this enum's integers intentionally differ from the
  /// backend `MobileAppRole` (here moderator=2/staff=3; backend Staff=2/
  /// Moderator=3, and backend has no Guest=0), so an int decode would mis-map —
  /// do not introduce a code path that emits/reads the role as an int.
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
