import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/live/youtube_url.dart';

// D-349 — the YouTube/HLS URL rule that drives the live player branch. Mirrors
// the C# LiveStreamUrlPolicy; keep the two in sync.
void main() {
  group('YoutubeUrl.tryParseId', () {
    test('extracts the id from a watch?v= link', () {
      expect(
        YoutubeUrl.tryParseId('https://www.youtube.com/watch?v=dQw4w9WgXcQ'),
        'dQw4w9WgXcQ',
      );
    });

    test('extracts the id from a youtu.be short link', () {
      expect(
        YoutubeUrl.tryParseId('https://youtu.be/dQw4w9WgXcQ'),
        'dQw4w9WgXcQ',
      );
    });

    test('extracts the id from a /live/ link', () {
      expect(
        YoutubeUrl.tryParseId('https://www.youtube.com/live/abc123XYZ_-'),
        'abc123XYZ_-',
      );
    });

    test('extracts the id from an /embed/ link', () {
      expect(
        YoutubeUrl.tryParseId('https://www.youtube.com/embed/abc123XYZ_-'),
        'abc123XYZ_-',
      );
    });

    test('extracts the id from a /shorts/ link', () {
      expect(
        YoutubeUrl.tryParseId('https://youtube.com/shorts/abc123XYZ_-'),
        'abc123XYZ_-',
      );
    });

    test('handles the m.youtube.com host', () {
      expect(
        YoutubeUrl.tryParseId('https://m.youtube.com/watch?v=dQw4w9WgXcQ'),
        'dQw4w9WgXcQ',
      );
    });

    test('returns null for an HLS stream url', () {
      expect(
        YoutubeUrl.tryParseId('https://live.example.sa/stream.m3u8'),
        isNull,
      );
    });

    test('returns null for a non-youtube url', () {
      expect(YoutubeUrl.tryParseId('https://vimeo.com/12345'), isNull);
    });

    test('returns null for blank / garbage input', () {
      expect(YoutubeUrl.tryParseId(null), isNull);
      expect(YoutubeUrl.tryParseId(''), isNull);
      expect(YoutubeUrl.tryParseId('not a url'), isNull);
    });
  });

  group('YoutubeUrl.isAllowedLiveUrl', () {
    test('accepts YouTube, HLS and MP4', () {
      expect(YoutubeUrl.isAllowedLiveUrl('https://youtu.be/x'), isTrue);
      expect(YoutubeUrl.isAllowedLiveUrl('https://live.example.sa/s.m3u8'), isTrue);
      expect(YoutubeUrl.isAllowedLiveUrl('https://cdn.example.sa/s.mp4'), isTrue);
    });

    test('rejects other urls, non-http schemes and blanks', () {
      expect(YoutubeUrl.isAllowedLiveUrl('https://vimeo.com/12345'), isFalse);
      expect(YoutubeUrl.isAllowedLiveUrl('ftp://host/s.mp4'), isFalse);
      expect(YoutubeUrl.isAllowedLiveUrl(''), isFalse);
      expect(YoutubeUrl.isAllowedLiveUrl(null), isFalse);
    });
  });
}
