import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';

void main() {
  group('ProfileRepository.mimeForFilename', () {
    test('maps the picker extensions to the MIME the server gate accepts', () {
      // The server checks Content-Type against image/jpeg|png|webp (+ magic
      // bytes); a wrong/missing MIME makes every upload 400, so the derivation
      // is the load-bearing part of the fix.
      expect(ProfileRepository.mimeForFilename('id.jpg'), 'image/jpeg');
      expect(ProfileRepository.mimeForFilename('ID.JPEG'), 'image/jpeg');
      expect(ProfileRepository.mimeForFilename('scan.png'), 'image/png');
      expect(ProfileRepository.mimeForFilename('photo.WEBP'), 'image/webp');
    });

    test('returns null for an unsupported extension', () {
      expect(ProfileRepository.mimeForFilename('doc.pdf'), isNull);
      expect(ProfileRepository.mimeForFilename('noext'), isNull);
    });
  });
}
