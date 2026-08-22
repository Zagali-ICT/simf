import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/image_upload_mime.dart';

void main() {
  group('imageUploadMime', () {
    test('maps the picker extensions to the MIME the server gate accepts', () {
      // The server checks Content-Type against image/jpeg|png|webp (+ magic
      // bytes); a wrong/missing MIME makes every upload 400, so the derivation
      // is the load-bearing part of the fix.
      expect(imageUploadMime('id.jpg'), 'image/jpeg');
      expect(imageUploadMime('ID.JPEG'), 'image/jpeg');
      expect(imageUploadMime('scan.png'), 'image/png');
      expect(imageUploadMime('photo.WEBP'), 'image/webp');
    });

    test('returns null for an unsupported extension', () {
      expect(imageUploadMime('doc.pdf'), isNull);
      expect(imageUploadMime('noext'), isNull);
    });

    test('never guesses image/jpeg for an unknown extension', () {
      // The regression this helper exists to close: the avatar upload used to
      // default every unrecognised extension to image/jpeg while the identity
      // document and staff uploads returned null, so one file behaved three
      // ways depending on which screen sent it.
      expect(imageUploadMime('avatar.gif'), isNull);
      expect(imageUploadMime('avatar.heic'), isNull);
    });
  });
}
