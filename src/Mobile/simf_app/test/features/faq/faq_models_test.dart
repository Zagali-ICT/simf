import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/faq/data/faq_models.dart';

void main() {
  group('FaqGroup.fromJson', () {
    test('parses a group with its nested entries', () {
      final group = FaqGroup.fromJson(<String, dynamic>{
        'id': 'g1',
        'name': 'Registration',
        'nameArabic': 'التسجيل',
        'entries': <dynamic>[
          <String, dynamic>{
            'id': 'e1',
            'question': 'How do I register?',
            'questionArabic': 'كيف أسجّل؟',
            'answer': 'Use the website.',
            'answerArabic': 'عبر الموقع.',
          },
        ],
      });

      expect(group.id, 'g1');
      expect(group.localizedName(true), 'التسجيل');
      expect(group.localizedName(false), 'Registration');
      expect(group.entries, hasLength(1));
      expect(group.entries.first.localizedQuestion(true), 'كيف أسجّل؟');
      expect(group.entries.first.localizedAnswer(false), 'Use the website.');
    });

    test('missing entries decodes to an empty list', () {
      final group = FaqGroup.fromJson(<String, dynamic>{
        'id': 'g2',
        'name': 'Venue',
        'nameArabic': 'المكان',
      });
      expect(group.entries, isEmpty);
    });

    test('localized getters fall back when one language is blank', () {
      const entry = FaqEntry(
        id: 'e',
        question: 'English only',
        questionArabic: '',
        answer: '',
        answerArabic: 'عربي فقط',
      );
      // Arabic question is blank → falls back to English.
      expect(entry.localizedQuestion(true), 'English only');
      // English answer is blank → falls back to Arabic.
      expect(entry.localizedAnswer(false), 'عربي فقط');
    });
  });
}
