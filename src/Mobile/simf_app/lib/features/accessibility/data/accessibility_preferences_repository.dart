import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'accessibility_controller.dart';

/// `accessibility-server-sync` — the server half of the accessibility choices.
///
/// The five flags (text size, high contrast, reduce motion, screen-reader
/// assist, captions) used to live in device prefs ONLY, so they did not follow
/// the user to a second device and did not survive a reinstall. They are now
/// account preferences: written through on every change and read back at
/// sign-in. The device prefs stay as the **offline cache** — every read still
/// comes from them, so the app renders the right scale on the first frame,
/// offline, before any network call.
///
/// Wire shape (`/api/v1/app/account/preferences`, `RequireApprovedAccount`):
/// `{ textSize, highContrast, reduceMotion, screenReaderAssist, captions }`,
/// where `textSize` is the stable [AppTextSize] name (`small` / `normal` /
/// `large` / `extraLarge`) — never an index, so the enum can be reordered.
class AccessibilityPreferencesRepository {
  AccessibilityPreferencesRepository(this._client);

  static const String _path = '/app/account/preferences';

  final SimfApiClient _client;

  /// `GET /app/account/preferences` → the account's stored choices, or **null**
  /// when the account has never saved any (`configured: false`).
  ///
  /// The null is load-bearing, not a convenience. The server answers a
  /// never-saved account with the DEFAULTS, which are indistinguishable from a
  /// user who deliberately chose them — and [AccessibilitySync.hydrate] applies
  /// whatever comes back over the device cache. Collapsing the two would reset a
  /// low-vision user who had already set extraLarge + high contrast locally (set
  /// before this endpoint existed, or set offline so the write-through failed
  /// silently) the first time they signed in. Null means "the server holds
  /// nothing" and the device's own choices win.
  Future<AccessibilitySettings?> fetch() {
    return _client.get<AccessibilitySettings?>(
      _path,
      decodeData: decodeOrNull,
    );
  }

  /// `PUT /app/account/preferences` → store [settings] against the account and
  /// return the persisted result.
  Future<AccessibilitySettings> save(AccessibilitySettings settings) {
    return _client.put<AccessibilitySettings>(
      _path,
      body: <String, dynamic>{
        'textSize': settings.textSize.name,
        'highContrast': settings.highContrast,
        'reduceMotion': settings.reduceMotion,
        'screenReaderAssist': settings.screenReaderAssist,
        'captions': settings.captions,
      },
      decodeData: decode,
    );
  }

  /// [decode], but yields null when the payload says `configured: false` — the
  /// account has never saved a choice, so there is nothing to apply.
  ///
  /// A payload with no `configured` field at all is treated as configured, so an
  /// older server that predates the flag still behaves as it used to.
  static AccessibilitySettings? decodeOrNull(Object? data) {
    final json =
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    if (json['configured'] as bool? ?? true) {
      return decode(data);
    }
    return null;
  }

  /// Decodes the envelope's data object. Every field is optional and falls back
  /// to the shipped default, so an older server payload (or a partially filled
  /// row) never throws — it just leaves that choice at its default.
  static AccessibilitySettings decode(Object? data) {
    final json =
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    return AccessibilitySettings(
      textSize: _textSizeFromName(json['textSize'] as String?),
      highContrast: json['highContrast'] as bool? ?? false,
      reduceMotion: json['reduceMotion'] as bool? ?? false,
      screenReaderAssist: json['screenReaderAssist'] as bool? ?? false,
      captions: json['captions'] as bool? ?? true,
    );
  }

  static AppTextSize _textSizeFromName(String? name) {
    for (final value in AppTextSize.values) {
      if (value.name == name) {
        return value;
      }
    }
    return AppTextSize.normal;
  }
}

final accessibilityPreferencesRepositoryProvider =
    Provider<AccessibilityPreferencesRepository>((ref) {
  return AccessibilityPreferencesRepository(ref.watch(simfApiClientProvider));
});
