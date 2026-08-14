import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'package:simf_data_pkg/src/storage/storage_keys.dart';

/// Thin wrapper over `flutter_secure_storage` for the tokens and any other
/// secret persisted by the app.
///
/// On iOS the underlying store is the Keychain; on Android the Keystore
/// (SIMF-MAA-001 §4 and §9.4). On web — a dev-diagnostics `flutter run -d
/// chrome` only, since SIMF-MAA-001 §2 ships Android + iOS — there is no
/// Keychain/Keystore: `flutter_secure_storage` falls back to browser storage,
/// which is **not encrypted**, so a web run must use throwaway / non-production
/// credentials only. The native-only `AndroidOptions`/`IOSOptions` are dropped
/// on web because they have no backend there.
class SimfSecureStorage {
  SimfSecureStorage({FlutterSecureStorage? storage})
      : _storage = storage ??
            (kIsWeb
                ? const FlutterSecureStorage()
                : const FlutterSecureStorage(
                    aOptions:
                        AndroidOptions(encryptedSharedPreferences: true),
                    iOptions: IOSOptions(
                      accessibility:
                          KeychainAccessibility.first_unlock_this_device,
                    ),
                  ));

  final FlutterSecureStorage _storage;

  Future<String?> read(String key) => _storage.read(key: key);

  Future<void> write(String key, String value) =>
      _storage.write(key: key, value: value);

  Future<void> delete(String key) => _storage.delete(key: key);

  /// Removes every secure value the app has written. Called on sign-out and
  /// when refresh fails permanently.
  Future<void> clearAuthValues() async {
    await Future.wait(<Future<void>>[
      delete(StorageKeys.accessToken),
      delete(StorageKeys.refreshToken),
      delete(StorageKeys.accessTokenExpiresAtIso),
      delete(StorageKeys.currentUserJson),
    ]);
  }
}
