import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/live/data/live_models.dart';

/// `LiveContentView.build` decided this inline, so the fallback branch — the
/// one that matters operationally — had no test.
LiveSession _session({DateTime? start, DateTime? end}) => LiveSession(
      title: 'Opening',
      titleArabic: 'الافتتاح',
      status: 1,
      hasRecording: false,
      start: start,
      end: end,
    );

void main() {
  group('LiveSession.isLiveAt', () {
    final start = DateTime.utc(2026, 11, 24, 10);
    final end = DateTime.utc(2026, 11, 24, 11);
    final windowed = _session(start: start, end: end);

    test('is live between the start and the end', () {
      expect(
        windowed.isLiveAt(DateTime.utc(2026, 11, 24, 10, 30), hasFeed: true),
        isTrue,
      );
    });

    test('the start is inclusive, the end exclusive', () {
      expect(windowed.isLiveAt(start, hasFeed: true), isTrue);
      expect(windowed.isLiveAt(end, hasFeed: true), isFalse);
    });

    test('is not live before or after the window, even with a feed up', () {
      expect(
        windowed.isLiveAt(DateTime.utc(2026, 11, 24, 9), hasFeed: true),
        isFalse,
      );
      expect(
        windowed.isLiveAt(DateTime.utc(2026, 11, 24, 12), hasFeed: true),
        isFalse,
      );
    });

    test('a windowed session ignores hasFeed', () {
      expect(
        windowed.isLiveAt(DateTime.utc(2026, 11, 24, 10, 30), hasFeed: false),
        isTrue,
      );
    });

    test('without a window it falls back to whether there is a feed', () {
      // The CP can publish a stream before the programme carries its times.
      final untimed = _session();
      expect(untimed.isLiveAt(start, hasFeed: true), isTrue);
      expect(untimed.isLiveAt(start, hasFeed: false), isFalse);
    });

    test('a half-open window counts as no window', () {
      expect(
        _session(start: start).isLiveAt(start, hasFeed: false),
        isFalse,
      );
      expect(
        _session(end: end).isLiveAt(start, hasFeed: true),
        isTrue,
      );
    });
  });
}
