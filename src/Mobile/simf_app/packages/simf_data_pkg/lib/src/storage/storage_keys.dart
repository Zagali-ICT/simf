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

  /// D-736 — soft-update snooze: the latest version the user dismissed on the
  /// splash prompt + when, so the same version doesn't re-nag for a few days.
  static const String appUpdateSnoozedVersion =
      'simf.prefs.app_update_snoozed_version';
  static const String appUpdateSnoozedAtIso =
      'simf.prefs.app_update_snoozed_at_iso';

  /// G-4 — on-device backlog of gate scans that could not reach the server
  /// (network down / timeout / a 5xx), held for automatic idempotent retry so
  /// an admitted person is never dropped. Value is a JSON array of
  /// PendingGateScan.
  static const String pendingGateScans = 'simf.prefs.pending_gate_scans';

  /// D-810 — the cached offline scanning rules for this operator's gates
  /// (allowed profile-type codes + the badge key), so a scanner that boots into
  /// a dead network still has the last known rules rather than nothing. Value is
  /// a JSON GateOfflineConfig.
  static const String gateOfflineConfig = 'simf.prefs.gate_offline_config';
}
