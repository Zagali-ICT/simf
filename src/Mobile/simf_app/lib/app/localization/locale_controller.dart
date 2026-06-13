import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Holds the current UI locale and persists the chosen language to prefs.
/// Arabic is the default (SIMF-MAA-001 §10).
class LocaleController extends Notifier<Locale> {
  LocaleController({required this.prefs});

  final SimfPrefsStorage prefs;

  @override
  Locale build() {
    final stored = prefs.getString(StorageKeys.preferredLanguage);
    if (stored == 'en') {
      return const Locale('en');
    }
    return const Locale('ar');
  }

  Future<void> setLanguage(String code) async {
    final next = code == 'en' ? const Locale('en') : const Locale('ar');
    await prefs.setString(StorageKeys.preferredLanguage, next.languageCode);
    state = next;
  }

  /// Flips AR ↔ EN — the language-toggle controls' single code path.
  Future<void> toggle() =>
      setLanguage(state.languageCode == 'ar' ? 'en' : 'ar');
}

final localeControllerProvider =
    NotifierProvider<LocaleController, Locale>(() {
  throw UnimplementedError(
    'localeControllerProvider must be overridden at app startup with a '
    'LocaleController whose prefs comes from simfPrefsStorageProvider.',
  );
});
