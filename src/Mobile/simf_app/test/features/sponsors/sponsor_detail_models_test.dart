import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';

void main() {
  group('SponsorDetail.fromData', () {
    test('decodes the about, city, tier, website and country', () {
      final detail = SponsorDetail.fromData(<String, dynamic>{
        'id': 's1',
        'nameEn': 'Aramco',
        'nameAr': 'أرامكو السعودية',
        'tier': 10,
        'tierName': 'Platinum',
        'logoRelativePath': 'logos/s1.png',
        'url': 'https://aramco.com',
        'about': 'A global energy company.',
        'aboutArabic': 'شركة طاقة عالمية.',
        'city': 'Dhahran',
        'cityArabic': 'الظهران',
        'countryId': 682,
        'countryNameEn': 'Saudi Arabia',
        'countryNameAr': 'المملكة العربية السعودية',
      });

      expect(detail.id, 's1');
      expect(detail.tier, 10);
      expect(detail.tierName, 'Platinum');
      expect(detail.url, 'https://aramco.com');
      expect(detail.localizedName(true), 'أرامكو السعودية');
      expect(detail.localizedAbout(false), 'A global energy company.');
      expect(detail.localizedAbout(true), 'شركة طاقة عالمية.');
      expect(detail.localizedCity(true), 'الظهران');
      expect(detail.localizedCountry(true), 'المملكة العربية السعودية');
    });

    test('optional fields fall back / decode to null', () {
      final detail = SponsorDetail.fromData(<String, dynamic>{
        'id': 's2',
        'nameEn': 'Co',
        'nameAr': '',
        'tier': 40,
        'tierName': 'Bronze',
      });
      expect(detail.localizedName(true), 'Co'); // falls back to EN
      expect(detail.localizedAbout(false), isNull);
      expect(detail.localizedCity(false), isNull);
      expect(detail.url, isNull);
    });
  });
}
