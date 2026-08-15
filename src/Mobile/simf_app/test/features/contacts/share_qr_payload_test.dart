import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/contacts/data/share_qr_payload.dart';

void main() {
  const vcard = 'BEGIN:VCARD\r\n'
      'VERSION:3.0\r\n'
      'FN:Raed\r\n'
      'END:VCARD';

  group('isVCardPayload', () {
    test('true for a vCard', () => expect(isVCardPayload(vcard), isTrue));
    test('false for a bare token',
        () => expect(isVCardPayload('ABC123'), isFalse),);
    test('tolerates leading whitespace/case', () {
      expect(isVCardPayload('  begin:vcard\nEND:VCARD'), isTrue);
    });
  });

  group('buildShareQrPayload', () {
    test('injects the token just before END:VCARD', () {
      final out = buildShareQrPayload(vcard, 'TOK123');
      expect(out.contains('X-SIMF-TOKEN:TOK123'), isTrue);
      expect(out.indexOf('X-SIMF-TOKEN:'), lessThan(out.indexOf('END:VCARD')));
      // The vCard is otherwise intact.
      expect(out.startsWith('BEGIN:VCARD'), isTrue);
      expect(out.trimRight().endsWith('END:VCARD'), isTrue);
    });

    test('preserves the CRLF line ending', () {
      final out = buildShareQrPayload(vcard, 'TOK123');
      expect(out.contains('X-SIMF-TOKEN:TOK123\r\nEND:VCARD'), isTrue);
    });

    test('uses LF when the source uses LF', () {
      const lf = 'BEGIN:VCARD\nFN:Raed\nEND:VCARD';
      final out = buildShareQrPayload(lf, 'TOK');
      expect(out.contains('X-SIMF-TOKEN:TOK\nEND:VCARD'), isTrue);
    });

    test('returns the vCard unchanged for an empty token', () {
      expect(buildShareQrPayload(vcard, ''), vcard);
      expect(buildShareQrPayload(vcard, '   '), vcard);
    });

    test('returns a non-vCard input unchanged', () {
      expect(buildShareQrPayload('ABC123', 'TOK'), 'ABC123');
    });
  });

  group('extractShareToken', () {
    test('reads the token back from a built payload (round-trip)', () {
      final out = buildShareQrPayload(vcard, 'TOK123');
      expect(extractShareToken(out), 'TOK123');
    });

    test('null for a foreign vCard with no token', () {
      expect(extractShareToken(vcard), isNull);
    });

    test('case-insensitive property name, trims value', () {
      const p = 'BEGIN:VCARD\nx-simf-token:  ZZZ \nEND:VCARD';
      expect(extractShareToken(p), 'ZZZ');
    });
  });
}
