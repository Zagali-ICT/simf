import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:device_info_plus/device_info_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Builds the label sent with a biometric enrolment.
///
/// Every device key ever enrolled carried the hardcoded default `SIMF mobile`,
/// because `enrolDeviceKey`'s `label` parameter existed and no caller ever
/// supplied it. Two devices under one account were therefore indistinguishable,
/// including to an administrator revoking one, and the label already reaches the
/// `DeviceKeyRegistered` audit detail.
///
/// **A hardware serial is not obtainable.** `device_info_plus` returns `unknown`
/// on Android unless the app meets Android's privileged requirements, and
/// `IosDeviceInfo` exposes no serial at all. So the second half of the label is a
/// *fingerprint*: the OS serial where a deployment does grant it, else iOS
/// `identifierForVendor`, else a UUID minted once into secure storage. Eight hex
/// characters of its SHA-256 reach the label, which is ample to tell a handful of
/// rows apart while storing nothing reversible.
///
/// `device_info_plus` deliberately stays out of `simf_auth_pkg`: that package is
/// auth and transport, and platform plugins do not belong in it. The label is
/// resolved here and passed into the `label` parameter that already exists.
class DeviceLabel {
  const DeviceLabel(this._secureStorage, {DeviceInfoPlugin? deviceInfo})
      : _deviceInfo = deviceInfo;

  final SimfSecureStorage _secureStorage;
  final DeviceInfoPlugin? _deviceInfo;

  /// The server rejects anything longer, so the guard is mandatory.
  static const int maxLength = 64;

  /// Today's constant, kept as the last resort. Losing an enrolment because a
  /// device-name lookup threw would be a worse defect than the one being fixed:
  /// the user has just completed an emailed code and an OS confirmation.
  static const String fallbackName = 'SIMF mobile';

  static const String _separator = ' · ';

  /// `{name} · {8 hex}`, for example `Galaxy S23 · 3e4f5a6b`.
  Future<String> resolve() async {
    final name = _sanitize(await _deviceName());
    final fingerprint = await _fingerprint();
    final room = maxLength - _separator.length - fingerprint.length;
    final trimmed = name.length > room ? name.substring(0, room) : name;
    return '$trimmed$_separator$fingerprint';
  }

  Future<String> _deviceName() async {
    final plugin = _deviceInfo ?? DeviceInfoPlugin();
    try {
      if (Platform.isAndroid) {
        final info = await plugin.androidInfo;
        return '${info.manufacturer} ${info.model}'.trim();
      }
      if (Platform.isIOS) {
        final info = await plugin.iosInfo;
        final model = info.modelName.trim();
        return model.isNotEmpty ? model : info.utsname.machine.trim();
      }
    } catch (_) {
      // A plugin failure must never block an enrolment. Fall through.
    }
    return fallbackName;
  }

  /// Resolution order: iOS `identifierForVendor`, else a random value minted
  /// once and reused.
  ///
  /// **There is no Android branch, and that is not an omission.**
  /// `AndroidDeviceInfo` exposes no serial number and no per-device identifier
  /// at all: `id` is a build changelist and `fingerprint` is shared by every
  /// handset of that model and build. Android's own serial API needs privileged
  /// access a store app does not have. So on Android the minted value IS the
  /// identifier, which is also the privacy-cleanest option available.
  Future<String> _fingerprint() async {
    final stored = await _readStoredFingerprint();
    if (stored != null) {
      return stored;
    }

    final source = await _fingerprintSource() ?? _mintFingerprintSource();
    final digest = sha256.convert(utf8.encode(source)).toString();
    final short = digest.substring(0, 8);
    try {
      await _secureStorage.write(StorageKeys.deviceFingerprint, short);
    } catch (_) {
      // A storage failure only costs stability across enrolments, not the label.
    }
    return short;
  }

  Future<String?> _readStoredFingerprint() async {
    try {
      final stored = await _secureStorage.read(StorageKeys.deviceFingerprint);
      return (stored != null && stored.isNotEmpty) ? stored : null;
    } catch (_) {
      return null;
    }
  }

  Future<String?> _fingerprintSource() async {
    if (!Platform.isIOS) {
      return null;
    }
    final plugin = _deviceInfo ?? DeviceInfoPlugin();
    try {
      final vendorId = (await plugin.iosInfo).identifierForVendor?.trim();
      if (vendorId != null && vendorId.isNotEmpty) {
        return vendorId;
      }
    } on Object {
      // Fall through to the minted value.
    }
    return null;
  }

  /// 16 random bytes as hex. Not a UUID library call: the value is hashed
  /// immediately and never leaves the device, so the format is irrelevant and a
  /// dependency for it would not earn its place.
  String _mintFingerprintSource() {
    final random = Random.secure();
    return List<int>.generate(16, (_) => random.nextInt(256))
        .map((byte) => byte.toRadixString(16).padLeft(2, '0'))
        .join();
  }

  /// The label is interpolated into an audit detail shaped
  /// `key=value; key=value`, and the server now rejects these characters
  /// outright. A manufacturer string is not trusted input, so strip rather than
  /// let a legitimate enrolment fail on a vendor's punctuation.
  static String _sanitize(String value) {
    final cleaned = value
        .replaceAll(RegExp(r'[;=\x00-\x1f\x7f]'), ' ')
        .replaceAll(RegExp(r'\s+'), ' ')
        .trim();
    return cleaned.isEmpty ? fallbackName : cleaned;
  }
}

final deviceLabelProvider = Provider<DeviceLabel>(
  (ref) => DeviceLabel(ref.read(simfSecureStorageProvider)),
);
