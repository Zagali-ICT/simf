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

  // ── Sessions / summaries ────────────────────────────────────────────────
  static const String sessionClock = 'assets/icons/session_clock.svg';
  static const String sessionUsers = 'assets/icons/session_users.svg';
  static const String sessionLocation = 'assets/icons/session_location.svg';
  static const String heartFilled = 'assets/icons/heart_filled.svg';
  static const String heartOutline = 'assets/icons/heart_outline.svg';
  static const String icLocation = 'assets/icons/ic_location.svg';
  static const String navUser = 'assets/icons/nav_user.svg';
  static const String icSearch = 'assets/icons/ic_search.svg';

  // ── Social (follow-us) ──────────────────────────────────────────────────
  static const String socialX = 'assets/icons/social_x.svg';
  static const String socialInstagram = 'assets/icons/social_instagram.svg';
  static const String socialLinkedin = 'assets/icons/social_linkedin.svg';
  static const String socialYoutube = 'assets/icons/social_youtube.svg';
  static const String socialTiktok = 'assets/icons/social_tiktok.svg';

  // ── Brand ───────────────────────────────────────────────────────────────
  /// The in-app brand mark. NOT the launcher icon, which is generated from
  /// `icon/app_icon.png` at build time and is never referenced from Dart.
  static const String simfLogo = 'assets/images/simf_logo.png';

  // ── Bottom navigation (Figma 206:1732) ──────────────────────────────────
  static const String navHome = 'assets/icons/nav_home.svg';
  static const String navCalendar = 'assets/icons/nav_calendar.svg';
  static const String navQr = 'assets/icons/nav_qr.svg';

  // ── Shared controls ─────────────────────────────────────────────────────
  static const String icTuning = 'assets/icons/ic_tuning.svg';
  static const String shareContact = 'assets/icons/share_contact.svg';

  // ── Home ────────────────────────────────────────────────────────────────
  static const String discoverHero = 'assets/images/discover_hero.jpg';

  // Home navigation tiles (KSA frame 758:1134). These are iconify glyphs
  // (solar / ph / streamline) bundled as SVGs because they have no 1:1
  // Material equivalent.
  static const String homeAboutSessions =
      'assets/icons/home_about_sessions.svg';
  static const String homeAiAssistant = 'assets/icons/home_ai_assistant.svg';
  static const String homeArchive = 'assets/icons/home_archive.svg';
  static const String homeAskModerator = 'assets/icons/home_ask_moderator.svg';
  static const String homeBadge = 'assets/icons/home_badge.svg';
  static const String homeBilateral = 'assets/icons/home_bilateral.svg';
  static const String homeBooths = 'assets/icons/home_booths.svg';
  static const String homeDelegations = 'assets/icons/home_delegations.svg';
  static const String homeExhibition = 'assets/icons/home_exhibition.svg';
  static const String homeMap = 'assets/icons/home_map.svg';
  static const String homeMeetPeople = 'assets/icons/home_meet_people.svg';
  static const String homePeople = 'assets/icons/home_people.svg';
  static const String homeSessions = 'assets/icons/home_sessions.svg';
  static const String homeSessionSummary =
      'assets/icons/home_session_summary.svg';
  static const String homeSpeakers = 'assets/icons/home_speakers.svg';

  // ── Requests / meetings ─────────────────────────────────────────────────
  static const String requestNew = 'assets/icons/request_new.svg';
  static const String requestLog = 'assets/icons/request_log.svg';
  static const String chevronLeft = 'assets/icons/chevron_left.svg';

  // ── Onboarding ──────────────────────────────────────────────────────────
  /// The single looping hero clip behind all three onboarding steps.
  ///
  /// It used to ship three times (`onboard_01..03.mp4`) as per-step
  /// placeholders, but the three files were byte-identical — ~13.8 MB of the
  /// same 4.6 MB clip in the APK, re-decoded from scratch on every swipe. One
  /// asset, one decoder (DEF-ONB-004). When the owner supplies genuinely
  /// different step clips, add them back as new constants and give the screen a
  /// per-step list again.
  static const String onboardVideo = 'assets/videos/onboard_01.mp4';
  static const String onboardingWorldMap =
      'assets/images/onboarding_world_map.jpg';
}
