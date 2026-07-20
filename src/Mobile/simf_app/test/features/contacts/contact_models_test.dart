import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';

// 2026-07-20 — bilingual JobTitle on the visitor contact / exhibitor cards:
// localizedJobTitle mirrors the existing localizedOrganisation idiom (Arabic
// primary in ar, English fallback, null when neither is set).
void main() {
  group('VisitorCard.localizedJobTitle', () {
    VisitorCard card({String? en, String? ar}) => VisitorCard(
          userId: 'u1',
          name: 'Name',
          nameArabic: 'الاسم',
          available: true,
          jobTitle: en,
          jobTitleArabic: ar,
        );

    test('picks per locale (Arabic primary in ar, English in en)', () {
      final c = card(en: 'Captain', ar: 'قائد');
      expect(c.localizedJobTitle(true), 'قائد');
      expect(c.localizedJobTitle(false), 'Captain');
    });

    test('falls back to the other language when one side is blank', () {
      expect(card(en: 'Captain').localizedJobTitle(true), 'Captain');
      expect(card(ar: 'قائد').localizedJobTitle(false), 'قائد');
    });

    test('is null when neither language is set', () {
      expect(card().localizedJobTitle(true), isNull);
      expect(card(en: '  ', ar: '').localizedJobTitle(false), isNull);
    });

    test('decodes jobTitleArabic from the wire (append-only)', () {
      final c = VisitorCard.fromData(<String, dynamic>{
        'userId': 'u1',
        'name': 'N',
        'nameArabic': 'ن',
        'available': true,
        'jobTitle': 'Captain',
        'jobTitleArabic': 'قائد',
      });
      expect(c.jobTitleArabic, 'قائد');
      expect(c.localizedJobTitle(true), 'قائد');
    });
  });

  group('SavedContactRow.localizedJobTitle', () {
    SavedContactRow row({String? en, String? ar}) => SavedContactRow(
          id: 's1',
          subjectUserId: 'u1',
          name: 'Name',
          nameArabic: 'الاسم',
          subjectAvailable: true,
          jobTitle: en,
          jobTitleArabic: ar,
        );

    test('picks per locale with fallback and null-when-empty', () {
      final r = row(en: 'Director', ar: 'مدير');
      expect(r.localizedJobTitle(true), 'مدير');
      expect(r.localizedJobTitle(false), 'Director');
      expect(row(en: 'Director').localizedJobTitle(true), 'Director');
      expect(row().localizedJobTitle(true), isNull);
    });

    test('decodes jobTitleArabic from the wire', () {
      final r = SavedContactRow.fromData(<String, dynamic>{
        'id': 's1',
        'subjectUserId': 'u1',
        'name': 'N',
        'nameArabic': 'ن',
        'subjectAvailable': true,
        'jobTitle': 'Director',
        'jobTitleArabic': 'مدير',
      });
      expect(r.jobTitleArabic, 'مدير');
    });
  });
}
