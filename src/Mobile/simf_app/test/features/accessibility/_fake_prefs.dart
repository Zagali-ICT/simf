import 'package:simf_data_pkg/simf_data_pkg.dart';

/// In-memory [SimfPrefsStorage] for tests — no platform channel. Optionally
/// seeded with starting values. Shared by the accessibility screen + controller
/// tests (same convention as `_fake_contacts_repo.dart`).
class FakePrefs implements SimfPrefsStorage {
  FakePrefs([Map<String, Object>? seed]) {
    if (seed != null) {
      _store.addAll(seed);
    }
  }

  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final value = _store[key];
    return value is String ? value : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) {
    final value = _store[key];
    return value is bool ? value : null;
  }

  @override
  Future<bool> setBool(String key, bool value) async {
    _store[key] = value;
    return true;
  }

  @override
  double? getDouble(String key) {
    final value = _store[key];
    return value is double ? value : null;
  }

  @override
  Future<bool> setDouble(String key, double value) async {
    _store[key] = value;
    return true;
  }

  @override
  int? getInt(String key) {
    final value = _store[key];
    return value is int ? value : null;
  }

  @override
  Future<bool> setInt(String key, int value) async {
    _store[key] = value;
    return true;
  }

  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}
