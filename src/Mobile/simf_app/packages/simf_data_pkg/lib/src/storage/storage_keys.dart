/// Single source of truth for storage key strings.
///
/// Every read and write into `SecureStorage` or `PrefsStorage` goes through
/// a constant declared here. Hardcoding a key string inline anywhere else is
/// a code-review defect — a typo turns into silent data loss.
class StorageKeys {
  StorageKeys._();

  // Secure storage — keys must never be logged or sent off-device.
  static const String accessToken = 'simf.auth.access_token';
  static const String refreshToken = 'simf.auth.refresh_token';
  static const String accessTokenExpiresAtIso =
      'simf.auth.access_token_expires_at_iso';
  static const String currentUserJson = 'simf.auth.current_user_json';

  // Device-key (biometric) — survives sign-out so a re-open can use it. NOT
  // cleared by clearAuthValues; cleared only when the device key is revoked.
  static const String deviceKeyId = 'simf.auth.device_key_id';
  static const String deviceKeyPrivate = 'simf.auth.device_key_private';

  // Preferences — non-sensitive app configuration.
  static const String preferredLanguage = 'simf.prefs.preferred_language';
  static const String preferredThemeMode = 'simf.prefs.preferred_theme_mode';
  static const String onboardingCompleted = 'simf.prefs.onboarding_completed';
  static const String hasAcceptedTerms = 'simf.prefs.has_accepted_terms';

  /// The last successfully-used sign-in email, pre-filled on the sign-in screen
  /// when the session has lapsed (Page_003 Logic L-3).
  static const String lastEmail = 'simf.prefs.last_email';
  static const String lastSeenNotificationsAt =
      'simf.prefs.last_seen_notifications_at_iso';
  static const String accessibilityTextSize =
      'simf.prefs.accessibility_text_size';
  static const String accessibilityHighContrast =
      'simf.prefs.accessibility_high_contrast';
  static const String accessibilityReduceMotion =
      'simf.prefs.accessibility_reduce_motion';
  static const String accessibilityScreenReader =
      'simf.prefs.accessibility_screen_reader';
  static const String accessibilityCaptions =
      'simf.prefs.accessibility_captions';

  /// D-495 — the cached Organization / About profile JSON (loaded at splash, read
  /// app-wide) + its `Last-Modified` token for the conditional-GET revalidation.
  static const String orgProfileJson = 'simf.prefs.org_profile_json';
  static const String orgProfileLastModified =
      'simf.prefs.org_profile_last_modified';
}
