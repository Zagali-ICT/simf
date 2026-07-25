import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/ai_summary/data/session_summary_models.dart';

void main() {
  group('SessionSummary.fromJson', () {
    test('decodes every field', () {
      final summary = SessionSummary.fromJson(const <String, dynamic>{
        'keyPoints': 'First point\nSecond point',
        'keyPointsArabic': 'نقطة أولى\nنقطة ثانية',
        'recommendations': 'Invest in coral reefs',
        'recommendationsArabic': 'الاستثمار في الشعاب المرجانية',
        'speakers': 'Dr Reef',
        'speakersArabic': 'د. ريف',
        'fullText': 'A long transcript summary.',
        'fullTextArabic': 'ملخص نصي طويل.',
        'generatedByAi': true,
        'publishedAt': '2026-11-23T07:30:00Z',
      });

      expect(summary.recommendations, 'Invest in coral reefs');
      expect(summary.localizedSpeakers(false), 'Dr Reef');
      expect(summary.localizedSpeakers(true), 'د. ريف');
      expect(summary.localizedFullText(false), 'A long transcript summary.');
      expect(summary.generatedByAi, isTrue);
      expect(summary.publishedAt, DateTime.utc(2026, 11, 23, 7, 30));
    });

    test('defaults missing fields and an unparsable timestamp', () {
      final summary = SessionSummary.fromJson(const <String, dynamic>{});

      expect(summary.keyPoints, '');
      expect(summary.generatedByAi, isFalse);
      expect(summary.publishedAt, isNull);
      expect(summary.publishedAt, isNull);
      expect(summary.keyPointsLines(false), isEmpty);
      // Item #35 — the two video URLs default to null (no players shown).
      expect(summary.recordingUrl, isNull);
      expect(summary.summaryVideoUrl, isNull);
    });

    // Item #35 — the summary surface carries two videos: the full live
    // recording and the team's short summary video, each from its own JSON key.
    test('decodes the recording + summary video urls when present', () {
      final summary = SessionSummary.fromJson(const <String, dynamic>{
        'recordingUrl': 'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
        'summaryVideoUrl': 'https://youtu.be/abcdefghijk',
      });

      expect(
        summary.recordingUrl,
        'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
      );
      expect(summary.summaryVideoUrl, 'https://youtu.be/abcdefghijk');
    });
  });

  group('keyPointsLines', () {
    test('splits a newline-delimited block and drops blank lines', () {
      final summary = SessionSummary.fromJson(const <String, dynamic>{
        'keyPoints': 'Alpha\n\n  Beta  \nGamma\n',
        'generatedByAi': true,
      });

      expect(
        summary.keyPointsLines(false),
        <String>['Alpha', 'Beta', 'Gamma'],
      );
    });

    test('reads the Arabic block when isArabic is true', () {
      final summary = SessionSummary.fromJson(const <String, dynamic>{
        'keyPoints': 'One\nTwo',
        'keyPointsArabic': 'واحد\nاثنان',
        'generatedByAi': true,
      });

      expect(summary.keyPointsLines(true), <String>['واحد', 'اثنان']);
      expect(summary.keyPointsLines(false), <String>['One', 'Two']);
    });

    test('falls back to the other language when one side is empty', () {
      final summary = SessionSummary.fromJson(const <String, dynamic>{
        'keyPoints': 'Only English',
        'generatedByAi': true,
      });

      // Arabic missing → falls back to the English block.
      expect(summary.keyPointsLines(true), <String>['Only English']);
    });
  });
}
