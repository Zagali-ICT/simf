// D-756 / D-761 — the hero background-video source gate.
// `HeroBackgroundVideo.isSupported` decides whether the home hero mounts the
// video (over the banner-image strip) or keeps the image fallback: only a direct
// https MP4/HLS stream is supported. A YouTube link is deliberately NOT supported
// (it would need an Android WebView, which can't be clipped into the hero band —
// D-761), so a YouTube URL falls back to the banner image. A blank /
// cleartext-http / non-video URL is not supported.
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/home/widgets/hero_background_video.dart';

void main() {
  group('HeroBackgroundVideo.isSupported', () {
    test('a direct https MP4/HLS stream is supported', () {
      expect(
        HeroBackgroundVideo.isSupported('https://cdn.example.com/hero.mp4'),
        isTrue,
      );
      expect(
        HeroBackgroundVideo.isSupported('https://cdn.example.com/live.m3u8'),
        isTrue,
      );
    });

    test('a YouTube link is NOT supported (falls back to the image band)', () {
      // A YouTube embed needs an Android WebView that cannot be clipped into the
      // short hero band (D-761), so a YouTube URL is excluded and the hero keeps
      // its banner-image / discover-photo fallback.
      expect(
        HeroBackgroundVideo.isSupported(
          'https://www.youtube.com/watch?v=rmW5sJTp-Zo',
        ),
        isFalse,
      );
      expect(
        HeroBackgroundVideo.isSupported('https://youtu.be/rmW5sJTp-Zo'),
        isFalse,
      );
    });

    test('a blank / null / unsupported URL is not supported', () {
      expect(HeroBackgroundVideo.isSupported(null), isFalse);
      expect(HeroBackgroundVideo.isSupported(''), isFalse);
      expect(HeroBackgroundVideo.isSupported('   '), isFalse);
      // Cleartext http is rejected (no downgrade).
      expect(
        HeroBackgroundVideo.isSupported('http://cdn.example.com/hero.mp4'),
        isFalse,
      );
      // A YouTube channel link has no video id.
      expect(
        HeroBackgroundVideo.isSupported('https://www.youtube.com/@simf'),
        isFalse,
      );
      // A plain page is neither YouTube nor a direct stream.
      expect(
        HeroBackgroundVideo.isSupported('https://example.com/not-a-video'),
        isFalse,
      );
    });
  });
}
