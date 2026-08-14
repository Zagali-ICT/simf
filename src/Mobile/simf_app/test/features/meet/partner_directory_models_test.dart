import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/meet/data/partner_directory_models.dart';

void main() {
  group('PartnerDirectoryEntry', () {
    test('fromJson maps every field', () {
      final entry = PartnerDirectoryEntry.fromJson(const <String, dynamic>{
        'kind': 'speaker',
        'id': 's1',
        'name': 'Sarah Hill',
        'nameArabic': 'سارة هل',
        'subtitle': 'Captain',
        'subtitleArabic': 'قبطان',
        'logoRelativePath': 'x.png',
        'logoContactId': null,
        'countryId': 682,
      });
      expect(entry.kind, 'speaker');
      expect(entry.id, 's1');
      expect(entry.name, 'Sarah Hill');
      expect(entry.nameArabic, 'سارة هل');
      expect(entry.subtitle, 'Captain');
      expect(entry.subtitleArabic, 'قبطان');
      expect(entry.logoRelativePath, 'x.png');
      expect(entry.countryId, 682);
      expect(entry.isSpeaker, isTrue);
    });

    test('listFromData reads the { entries: [...] } envelope', () {
      final list = PartnerDirectoryEntry.listFromData(<String, dynamic>{
        'entries': <dynamic>[
          <String, dynamic>{'kind': 'sponsor', 'id': 'p1', 'name': 'Acme', 'nameArabic': 'أكمي'},
          <String, dynamic>{'kind': 'booth', 'id': 'b1', 'name': 'Co', 'nameArabic': 'شركة'},
        ],
      });
      expect(list, hasLength(2));
      expect(list.first.isSponsor, isTrue);
      expect(list.last.isBooth, isTrue);
    });

    test('localizedName / localizedSubtitle fall back across languages', () {
      const arOnly = PartnerDirectoryEntry(
        kind: 'person', id: 'u1', name: '', nameArabic: 'محمد',
        subtitleArabic: 'مهندس',
      );
      expect(arOnly.localizedName(isArabic: false), 'محمد'); // en empty → ar fallback
      expect(arOnly.localizedName(isArabic: true), 'محمد');
      expect(arOnly.localizedSubtitle(isArabic: false), 'مهندس');
      expect(arOnly.localizedSubtitle(isArabic: true), 'مهندس');
      const noSub = PartnerDirectoryEntry(
        kind: 'person', id: 'u2', name: 'X', nameArabic: 'س',
      );
      expect(noSub.localizedSubtitle(isArabic: true), isNull);
    });

    test('logoUrl builds the right asset route per kind', () {
      const base = 'http://t/api/v1';
      const speaker = PartnerDirectoryEntry(
        kind: 'speaker', id: 's1', name: 'S', nameArabic: 'س',
        logoRelativePath: 'x.png',
      );
      const sponsor = PartnerDirectoryEntry(
        kind: 'sponsor', id: 'p1', name: 'P', nameArabic: 'ب',
        logoRelativePath: 'x.png',
      );
      const booth = PartnerDirectoryEntry(
        kind: 'booth', id: 'b1', name: 'B', nameArabic: 'ب',
        logoContactId: 'c9',
      );
      const person = PartnerDirectoryEntry(
        kind: 'person', id: 'u1', name: 'U', nameArabic: 'م',
      );
      const noLogoSpeaker = PartnerDirectoryEntry(
        kind: 'speaker', id: 's2', name: 'S', nameArabic: 'س',
      );

      expect(speaker.logoUrl(base), '$base/app/assets/SpeakerPhoto/s1/image');
      expect(sponsor.logoUrl(base), '$base/app/assets/SponsorLogo/p1/image');
      expect(booth.logoUrl(base), '$base/app/assets/CompanyLogo/c9/image');
      expect(person.logoUrl(base), isNull);
      // No uploaded logo → no URL (the cell shows initials).
      expect(noLogoSpeaker.logoUrl(base), isNull);
      // Booth with no exhibitor contact → no company logo.
      const boothNoContact = PartnerDirectoryEntry(
        kind: 'booth', id: 'b2', name: 'B', nameArabic: 'ب',
      );
      expect(boothNoContact.logoUrl(base), isNull);
    });
  });
}
