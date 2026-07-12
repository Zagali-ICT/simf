import 'dart:math';

/// Generates an RFC-4122 version-4 UUID in canonical 36-char form using a
/// cryptographically-secure RNG.
///
/// The gate scan idempotency key must be a fresh per-scan UUIDv4
/// (SIMF-API-GATES-001 §9; the server `ScanIdempotency.Key` contract is a
/// client UUIDv4). A stable/derived key makes a genuine re-entry of the same
/// badge in the same direction collide with the first scan's 24h replay window
/// and be silently swallowed (G-1) — so every scan mints a new key here.
String randomUuidV4() {
  final rng = Random.secure();
  final bytes = List<int>.generate(16, (_) => rng.nextInt(256));
  // Version 4 in the high nibble of byte 6; RFC-4122 variant (10xx) in byte 8.
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  final hex = <String>[
    for (final b in bytes) b.toRadixString(16).padLeft(2, '0'),
  ];
  return '${hex[0]}${hex[1]}${hex[2]}${hex[3]}-'
      '${hex[4]}${hex[5]}-'
      '${hex[6]}${hex[7]}-'
      '${hex[8]}${hex[9]}-'
      '${hex[10]}${hex[11]}${hex[12]}${hex[13]}${hex[14]}${hex[15]}';
}
