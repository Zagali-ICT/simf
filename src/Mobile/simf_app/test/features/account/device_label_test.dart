import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/device_label.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// An in-memory stand-in: the real one goes over a platform channel that never
/// completes under the test binding.
class _InMemSecureStorage implements SimfSecureStorage {
  final Map<String, String> values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async {
    values[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    values.remove(key);
  }

  @override
  Future<void> deleteAll() async => values.clear();

  @override
  Future<void> clearAuthValues() async => values.clear();
}

/// Storage that fails every call, standing in for a device whose keystore is
/// unavailable. Losing an enrolment to a fingerprint lookup would be worse than
/// the ambiguity the label exists to fix.
class _BrokenSecureStorage implements SimfSecureStorage {
  @override
  Future<String?> read(String key) async => throw Exception('unavailable');

  @override
  Future<void> write(String key, String value) async =>
      throw Exception('unavailable');

  @override
  Future<void> delete(String key) async => throw Exception('unavailable');

  @override
  Future<void> deleteAll() async => throw Exception('unavailable');

  @override
  Future<void> clearAuthValues() async => throw Exception('unavailable');
}

void main() {
  group('DeviceLabel', () {
    test('reuses a fingerprint already stored, so the suffix is stable', () async {
      final storage = _InMemSecureStorage()
        ..values[StorageKeys.deviceFingerprint] = 'abcd1234';

      final label = await DeviceLabel(storage).resolve();

      expect(label, endsWith('abcd1234'));
    });

    test('mints a fingerprint once and reuses it on the next enrolment',
        () async {
      final storage = _InMemSecureStorage();
      final subject = DeviceLabel(storage);

      final first = await subject.resolve();
      final second = await subject.resolve();

      expect(first, second);
      expect(storage.values[StorageKeys.deviceFingerprint], isNotNull);
    });

    test('the minted fingerprint is 8 hex characters', () async {
      final storage = _InMemSecureStorage();

      await DeviceLabel(storage).resolve();

      expect(
        storage.values[StorageKeys.deviceFingerprint],
        matches(RegExp(r'^[0-9a-f]{8}$')),
      );
    });

    test('never exceeds the 64-character column the server enforces', () async {
      final storage = _InMemSecureStorage();

      final label = await DeviceLabel(storage).resolve();

      expect(label.length, lessThanOrEqualTo(DeviceLabel.maxLength));
    });

    test('carries no character that could forge an audit field', () async {
      final storage = _InMemSecureStorage();

      final label = await DeviceLabel(storage).resolve();

      expect(label.contains(';'), isFalse);
      expect(label.contains('='), isFalse);
      expect(label.contains('\n'), isFalse);
    });

    test('still produces a usable label when secure storage throws', () async {
      final label = await DeviceLabel(_BrokenSecureStorage()).resolve();

      expect(label, isNotEmpty);
      expect(label.length, lessThanOrEqualTo(DeviceLabel.maxLength));
    });

    test('falls back to the old constant for the name off-device', () async {
      // The unit-test VM is neither Android nor iOS, so the platform branches
      // are skipped and the fallback is what remains. This pins the guarantee
      // that a device whose name cannot be read still enrols.
      final label = await DeviceLabel(_InMemSecureStorage()).resolve();

      expect(label, startsWith(DeviceLabel.fallbackName));
    });
  });
}
