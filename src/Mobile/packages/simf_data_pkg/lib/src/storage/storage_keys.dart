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

  // Preferences — non-sensitive app configuration.
  static const String preferredLanguage = 'simf.prefs.preferred_language';
  static const String preferredThemeMode = 'simf.prefs.preferred_theme_mode';
  static const String onboardingCompleted = 'simf.prefs.onboarding_completed';
  static const String hasAcceptedTerms = 'simf.prefs.has_accepted_terms';
  static const String lastSeenNotificationsAt =
      'simf.prefs.last_seen_notifications_at_iso';
  static const String accessibilityFontScale =
      'simf.prefs.accessibility_font_scale';
  static const String accessibilityHighContrast =
      'simf.prefs.accessibility_high_contrast';
}
