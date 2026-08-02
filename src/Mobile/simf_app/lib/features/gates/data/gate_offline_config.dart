import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-820 — the rules a scanner caches so it can judge a badge with no network.
///
/// Mirrors `SIMF.Contracts.Gates.GateOfflineConfig`. Fetched from
/// `GET /app/gates/offline-config` whenever the console loads and stored in
/// prefs, so a device that boots into a dead network still has yesterday's
/// rules rather than nothing.
class GateOfflineConfig {
  const GateOfflineConfig({
    required this.badgeKey,
    required this.badgeKeyVersion,
    required this.previousBadgeKey,
    required this.previousBadgeKeyVersion,
    required this.sessionWalkIn,
    required this.issuedAtIso,
    required this.gates,
  });

  factory GateOfflineConfig.fromJson(Map<String, dynamic> json) {
    return GateOfflineConfig(
      badgeKey: json['badgeKey'] as String?,
      badgeKeyVersion: (json['badgeKeyVersion'] as num?)?.toInt() ?? 0,
      previousBadgeKey: json['previousBadgeKey'] as String?,
      previousBadgeKeyVersion:
          (json['previousBadgeKeyVersion'] as num?)?.toInt() ?? 0,
      sessionWalkIn: json['sessionWalkIn'] as bool? ?? false,
      issuedAtIso: json['issuedAt'] as String? ?? '',
      gates: (json['gates'] as List?)
              ?.whereType<Map<dynamic, dynamic>>()
              .map((e) => GateOfflineRule.fromJson(e.cast<String, dynamic>()))
              .toList(growable: false) ??
          const <GateOfflineRule>[],
    );
  }

  final String? badgeKey;
  final int badgeKeyVersion;
  final String? previousBadgeKey;
  final int previousBadgeKeyVersion;

  /// True when the session-registration rule is relaxed, so a hall door admits
  /// an attendee this device has no booking record for.
  final bool sessionWalkIn;

  final String issuedAtIso;
  final List<GateOfflineRule> gates;

  /// True when this device can verify a badge at all. False means the
  /// capability is not armed and every offline scan is queued without a
  /// verdict, exactly as before D-820.
  bool get canVerifyOffline => keyFor(badgeKeyVersion) != null;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'badgeKey': badgeKey,
        'badgeKeyVersion': badgeKeyVersion,
        'previousBadgeKey': previousBadgeKey,
        'previousBadgeKeyVersion': previousBadgeKeyVersion,
        'sessionWalkIn': sessionWalkIn,
        'issuedAt': issuedAtIso,
        'gates': gates.map((g) => g.toJson()).toList(growable: false),
      };

  GateOfflineRule? ruleFor(String gateId) {
    for (final gate in gates) {
      if (gate.gateId == gateId) {
        return gate;
      }
    }
    return null;
  }

  /// The key for a badge's stamped version, so both keys work during a
  /// rotation window. Null for a version this device was not given.
  Uint8List? keyFor(int version) {
    if (version == badgeKeyVersion) {
      return _decodeKey(badgeKey);
    }
    if (version == previousBadgeKeyVersion) {
      return _decodeKey(previousBadgeKey);
    }
    return null;
  }

  static Uint8List? _decodeKey(String? encoded) {
    if (encoded == null || encoded.isEmpty) {
      return null;
    }
    final Uint8List bytes;
    try {
      bytes = base64Decode(encoded);
    } on FormatException {
      return null;
    }
    // Fail closed: a key that is not AES-256 disables offline verification
    // rather than being padded or truncated into something that "works".
    return bytes.length == 32 ? bytes : null;
  }
}

/// D-820 — the offline rule for one gate.
class GateOfflineRule {
  const GateOfflineRule({
    required this.gateId,
    required this.code,
    required this.allowedProfileTypeCodes,
    required this.isHallDoor,
    required this.isActive,
  });

  factory GateOfflineRule.fromJson(Map<String, dynamic> json) {
    return GateOfflineRule(
      gateId: json['gateId'] as String? ?? '',
      code: json['code'] as String? ?? '',
      allowedProfileTypeCodes: (json['allowedProfileTypeCodes'] as List?)
              ?.whereType<num>()
              .map((e) => e.toInt())
              .toList(growable: false) ??
          const <int>[],
      isHallDoor: json['isHallDoor'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? false,
    );
  }

  final String gateId;
  final String code;

  /// Empty means this gate admits every type — the same rule the server
  /// applies, where an empty allow-list is "no restriction", not "nobody".
  final List<int> allowedProfileTypeCodes;

  final bool isHallDoor;
  final bool isActive;

  bool admits(int profileTypeCode) =>
      allowedProfileTypeCodes.isEmpty ||
      allowedProfileTypeCodes.contains(profileTypeCode);

  Map<String, dynamic> toJson() => <String, dynamic>{
        'gateId': gateId,
        'code': code,
        'allowedProfileTypeCodes': allowedProfileTypeCodes,
        'isHallDoor': isHallDoor,
        'isActive': isActive,
      };
}

/// D-820 — the cached copy, in the non-sensitive prefs store alongside the scan
/// queue.
///
/// The badge key IS a secret and this store is not encrypted. That is the
/// owner's accepted trade for a single shared key both the desk and the scanner
/// hold; the compensating controls are that the key only travels while the
/// capability is armed, that it can be rotated by version, and that every scan
/// is reconciled after the event.
class GateOfflineConfigCache {
  const GateOfflineConfigCache(this._prefs);

  final SimfPrefsStorage _prefs;

  GateOfflineConfig? read() {
    final raw = _prefs.getString(StorageKeys.gateOfflineConfig);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map) {
        return null;
      }
      return GateOfflineConfig.fromJson(decoded.cast<String, dynamic>());
    } on FormatException {
      return null;
    }
  }

  Future<void> write(GateOfflineConfig config) => _prefs.setString(
        StorageKeys.gateOfflineConfig,
        jsonEncode(config.toJson()),
      );

  Future<void> clear() => _prefs.remove(StorageKeys.gateOfflineConfig);
}

final gateOfflineConfigCacheProvider = Provider<GateOfflineConfigCache>((ref) {
  return GateOfflineConfigCache(ref.watch(simfPrefsStorageProvider));
});
