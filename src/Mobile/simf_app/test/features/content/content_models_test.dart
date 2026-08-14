import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/content/data/content_models.dart';

void main() {
  group('ContentBlock', () {
    test('fromJson maps the wire fields', () {
      final block = ContentBlock.fromJson(const <String, dynamic>{
        'key': 'terms',
        'content': 'EN body',
        'contentArabic': 'النص العربي',
        'lastUpdatedAt': '2026-09-01T00:00:00Z',
      });

      expect(block.key, 'terms');
      expect(block.content, 'EN body');
      expect(block.contentArabic, 'النص العربي');
      expect(block.lastUpdatedAt, isNotNull);
      expect(block.hasBody, isTrue);
    });

    test('localizedBody picks the active locale and falls back', () {
      const both =
          ContentBlock(key: 'terms', content: 'EN', contentArabic: 'AR');
      expect(both.localizedBody(isArabic: true), 'AR');
      expect(both.localizedBody(isArabic: false), 'EN');

      const arOnly =
          ContentBlock(key: 'terms', content: '', contentArabic: 'AR');
      expect(arOnly.localizedBody(isArabic: false),
          'AR',); // EN empty → fall back to AR

      const enOnly =
          ContentBlock(key: 'terms', content: 'EN', contentArabic: '');
      expect(enOnly.localizedBody(isArabic: true),
          'EN',); // AR empty → fall back to EN
    });

    test('hasBody is false when both bodies are blank', () {
      const block =
          ContentBlock(key: 'terms', content: '   ', contentArabic: '');
      expect(block.hasBody, isFalse);
    });

    test('a missing lastUpdatedAt parses to null', () {
      final block = ContentBlock.fromJson(const <String, dynamic>{
        'key': 'terms',
        'content': 'x',
        'contentArabic': 'y',
      });
      expect(block.lastUpdatedAt, isNull);
    });
  });

  group('ContentBlock.bullets', () {
    // The Terms screen renders one bullet card per non-empty body line. That
    // split used to happen inside its build path, so nothing tested it.
    ContentBlock blockWith(String en) =>
        ContentBlock(key: 'terms', content: en, contentArabic: '');

    test('one bullet per non-empty line', () {
      final block = blockWith(<String>['first', 'second', 'third'].join('\n'));
      expect(
        block.bullets(isArabic: false),
        <String>['first', 'second', 'third'],
      );
    });

    test('drops blank and whitespace-only lines', () {
      final block = blockWith(<String>['first', '', '   ', 'second'].join('\n'));
      expect(block.bullets(isArabic: false), <String>['first', 'second']);
    });

    test('trims each bullet', () {
      final block = blockWith(<String>['  padded  ', ' tabbed '].join('\n'));
      expect(block.bullets(isArabic: false), <String>['padded', 'tabbed']);
    });

    test('an empty body yields no bullets, so the screen stays empty', () {
      expect(blockWith('').bullets(isArabic: false), isEmpty);
      expect(
        blockWith(<String>['  ', ''].join('\n')).bullets(isArabic: false),
        isEmpty,
      );
    });

    test('follows the active language', () {
      final block = ContentBlock(
        key: 'terms',
        content: <String>['en one', 'en two'].join('\n'),
        contentArabic: 'عربي',
      );
      expect(block.bullets(isArabic: true), <String>['عربي']);
      expect(block.bullets(isArabic: false), <String>['en one', 'en two']);
    });
  });
}
