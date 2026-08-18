import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Remembers which sessions have already shown the one-time "rate this session"
/// prompt, so a customer is asked to rate a given session **at most once**
/// (owner: "every session after view must show rate session … one time per
/// customer").
///
/// The shown session ids are persisted in [SimfPrefsStorage] (non-sensitive UI
/// state, like the accessibility + onboarding flags) as a single comma-joined
/// string — session ids are GUIDs, which never contain a comma, so the
/// delimiter is unambiguous. Persisting means the prompt does not re-appear on
/// a later launch either.
class SessionRatePromptTracker {
  SessionRatePromptTracker(this._prefs);

  final SimfPrefsStorage _prefs;

  /// The prefs key. Declared here (not in the data package's frozen
  /// `StorageKeys` catalogue, which is out of this feature's edit scope) but it
  /// follows the same `simf.prefs.*` naming convention.
  static const String _storageKey = 'simf.prefs.session_rate_prompt_shown';

  static const String _separator = ',';

  bool hasShown(String sessionId) => _shownIds().contains(sessionId);

  /// Records that the rate prompt has now been shown for [sessionId], so it is
  /// never shown again for that session on this device. A no-op for a blank id
  /// or one already recorded.
  Future<void> markShown(String sessionId) async {
    if (sessionId.isEmpty) {
      return;
    }
    final ids = _shownIds();
    if (ids.contains(sessionId)) {
      return;
    }
    ids.add(sessionId);
    await _prefs.setString(_storageKey, ids.join(_separator));
  }

  Set<String> _shownIds() {
    final raw = _prefs.getString(_storageKey);
    if (raw == null || raw.isEmpty) {
      return <String>{};
    }
    return raw.split(_separator).where((id) => id.isNotEmpty).toSet();
  }
}

final sessionRatePromptTrackerProvider =
    Provider<SessionRatePromptTracker>((ref) {
  return SessionRatePromptTracker(ref.watch(simfPrefsStorageProvider));
});
