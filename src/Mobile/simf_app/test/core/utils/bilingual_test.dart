import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/bilingual.dart';

/// One rule replacing fourteen private copies. These pin the fallback, which is
/// the part that was worth sharing: the CP lets a field be filled in one
/// language only, so the preferred side being blank must not render blank.
void main() {
  group('pickLocalized', () {
    test('prefers the active language when it is present', () {
      expect(pickLocalized('عربي', 'English', isArabic: true), 'عربي');
      expect(pickLocalized('عربي', 'English', isArabic: false), 'English');
    });

    test('falls back to the other language when the preferred is blank', () {
      expect(pickLocalized('', 'English', isArabic: true), 'English');
      expect(pickLocalized('عربي', '', isArabic: false), 'عربي');
    });

    test('treats whitespace-only as blank, in either position', () {
      expect(pickLocalized('   ', 'English', isArabic: true), 'English');
      expect(pickLocalized('عربي', '  \n ', isArabic: false), 'عربي');
    });

    test('treats null as blank', () {
      expect(pickLocalized(null, 'English', isArabic: true), 'English');
      expect(pickLocalized('عربي', null, isArabic: false), 'عربي');
    });

    test('returns empty only when both sides are blank', () {
      expect(pickLocalized('', '', isArabic: true), '');
      expect(pickLocalized(null, null, isArabic: false), '');
    });

    test('trims the value it returns', () {
      // faq_models and moderation_models returned the padded original.
      expect(pickLocalized('  ع  ', 'English', isArabic: true), 'ع');
      expect(pickLocalized('ع', '  English  ', isArabic: false), 'English');
    });
  });

  group('pickLocalizedOrNull', () {
    test('matches pickLocalized when there is a value', () {
      expect(pickLocalizedOrNull('عربي', 'English', isArabic: true), 'عربي');
      expect(pickLocalizedOrNull('', 'English', isArabic: true), 'English');
    });

    test('is null when both blank, where pickLocalized is empty', () {
      expect(pickLocalizedOrNull('', '', isArabic: true), isNull);
      expect(pickLocalizedOrNull('  ', null, isArabic: false), isNull);
      expect(pickLocalized('', '', isArabic: true), '');
    });
  });
}
