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

  // ── Booths / venue ──────────────────────────────────────────────────────
  static const String navLocation = 'assets/icons/nav_location.svg';

  // ── Speakers ────────────────────────────────────────────────────────────
  static const String speakerPlaceholder =
      'assets/icons/speaker_placeholder.svg';
  static const String icCaretLeft = 'assets/icons/ic_caret_left.svg';

  // ── Badge ───────────────────────────────────────────────────────────────
  static const String badgeScan = 'assets/icons/badge_scan.svg';

  // ── Requests / meetings ─────────────────────────────────────────────────
  static const String requestNew = 'assets/icons/request_new.svg';
  static const String requestLog = 'assets/icons/request_log.svg';
  static const String chevronLeft = 'assets/icons/chevron_left.svg';

  // ── Onboarding ──────────────────────────────────────────────────────────
  static const String onboardVideo1 = 'assets/videos/onboard_01.mp4';
  static const String onboardVideo2 = 'assets/videos/onboard_02.mp4';
  static const String onboardVideo3 = 'assets/videos/onboard_03.mp4';
  static const String onboardingWorldMap =
      'assets/images/onboarding_world_map.jpg';
}
