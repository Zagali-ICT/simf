import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_data_pkg/src/api/auth_token_source.dart';
import 'package:simf_data_pkg/src/api/simf_api_client.dart';
import 'package:simf_data_pkg/src/config/simf_data_config.dart';
import 'package:simf_data_pkg/src/storage/prefs_storage.dart';
import 'package:simf_data_pkg/src/storage/secure_storage.dart';

/// Riverpod providers exported by the data package.
///
/// The main app supplies the values that the data package cannot know about
/// itself (the config, the prefs instance, the auth token source) by
/// overriding the providers below at the `ProviderScope` root.
///
/// **Never** read `_uninitialised` directly — every override is mandatory
/// at app startup; an unset provider here means a setup bug, which we
/// surface as a clear error rather than a null reference later.

/// The current build-flavour configuration (base URL, app key, device type).
/// Overridden in `main.dart`.
final simfDataConfigProvider = Provider<SimfDataConfig>((ref) {
  throw UnimplementedError(
    'simfDataConfigProvider must be overridden at app startup '
    'with a SimfDataConfig for the current build flavour.',
  );
});

/// Returns `ar` or `en` for the current UI language. Overridden by the
/// main app to point at its localeController.
final currentLanguageCodeProvider = Provider<String Function()>((ref) {
  // Default to Arabic (the app's primary language, SIMF-MAA-001 §10) so
  // requests made before the language provider is overridden still carry
  // a valid Accept-Language value.
  return () => 'ar';
});

/// Implementation of [AuthTokenSource]. The auth package supplies this via
/// an override.
final authTokenSourceProvider = Provider<AuthTokenSource>((ref) {
  return const NoAuthTokenSource();
});

/// The app's single shared-preferences instance. The main app initialises
/// it during `main()` and overrides the provider with the resolved value.
final simfPrefsStorageProvider = Provider<SimfPrefsStorage>((ref) {
  throw UnimplementedError(
    'simfPrefsStorageProvider must be overridden at app startup with '
    'the SharedPreferences-backed instance.',
  );
});

/// The secure-storage wrapper. Defaults to a real instance; tests override.
final simfSecureStorageProvider = Provider<SimfSecureStorage>((ref) {
  return SimfSecureStorage();
});

/// The one SIMF API client. Every repository in the codebase reads it from
/// this provider; nothing else instantiates a Dio.
final simfApiClientProvider = Provider<SimfApiClient>((ref) {
  final config = ref.watch(simfDataConfigProvider);
  final tokenSource = ref.watch(authTokenSourceProvider);
  final languageCode = ref.watch(currentLanguageCodeProvider);
  return SimfApiClient.build(
    config: config,
    tokenSource: tokenSource,
    currentLanguageCode: languageCode,
  );
});
