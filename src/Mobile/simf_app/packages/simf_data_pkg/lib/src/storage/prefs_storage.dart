import 'package:shared_preferences/shared_preferences.dart';

/// Thin wrapper over `shared_preferences` for non-sensitive app configuration.
///
/// What lives here: UI language, theme mode, onboarding flag, accessibility
/// settings — anything that is "preferences", not secrets. Tokens never come
/// near this storage. See SIMF-MAA-001 §4.
class SimfPrefsStorage {
  SimfPrefsStorage(this._prefs);

  /// Convenience for app startup: read once and pass into the provider.
  static Future<SimfPrefsStorage> open() async {
    final prefs = await SharedPreferences.getInstance();
    return SimfPrefsStorage(prefs);
  }

  final SharedPreferences _prefs;

  String? getString(String key) => _prefs.getString(key);

  Future<bool> setString(String key, String value) =>
      _prefs.setString(key, value);

  bool? getBool(String key) => _prefs.getBool(key);

  Future<bool> setBool(String key, bool value) => _prefs.setBool(key, value);

  double? getDouble(String key) => _prefs.getDouble(key);

  Future<bool> setDouble(String key, double value) =>
      _prefs.setDouble(key, value);

  int? getInt(String key) => _prefs.getInt(key);

  Future<bool> setInt(String key, int value) => _prefs.setInt(key, value);

  Future<bool> remove(String key) => _prefs.remove(key);
}
