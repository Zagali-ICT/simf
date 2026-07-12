import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/uuid_v4.dart';

void main() {
  // The gate scan idempotency key is a fresh per-scan UUIDv4 (G-1); the server
  // ScanIdempotency.Key contract requires the canonical RFC-4122 v4 shape.
  final uuidV4 = RegExp(
    r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
  );

  group('randomUuidV4', () {
    test('is a canonical 36-char RFC-4122 version-4 UUID', () {
      final value = randomUuidV4();
      expect(value.length, 36);
      // Version nibble is 4; variant nibble is one of 8/9/a/b.
      expect(uuidV4.hasMatch(value), isTrue, reason: 'was: $value');
    });

    test('mints a fresh, distinct key on every call (1000 draws)', () {
      final seen = <String>{};
      for (var i = 0; i < 1000; i++) {
        final value = randomUuidV4();
        expect(uuidV4.hasMatch(value), isTrue, reason: 'was: $value');
        expect(seen.add(value), isTrue, reason: 'duplicate key: $value');
      }
      expect(seen.length, 1000);
    });
  });
}
