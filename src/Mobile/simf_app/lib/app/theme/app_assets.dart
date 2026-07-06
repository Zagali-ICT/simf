/// Centralised bundled-asset paths.
///
/// Every icon / image referenced from Dart code goes through a constant here so
/// a rename or move is a single edit and no screen hardcodes an `assets/...`
/// string (owner rule). Grouped by area; extend as other modules adopt it.
class AppAssets {
  AppAssets._();

  // ── Auth / account (Figma auth frames 627:2361 / 758:2555) ──────────────
  static const String authBack = 'assets/icons/auth_back.svg';
  static const String authGlobe = 'assets/icons/auth_globe.svg';
  static const String authEye = 'assets/icons/auth_eye.svg';
  static const String authEyeOff = 'assets/icons/auth_eye_off.svg';
  static const String authFaceId = 'assets/icons/auth_faceid.svg';

  // ── Shared navigation ───────────────────────────────────────────────────
  static const String icBack = 'assets/icons/ic_back.svg';
}
