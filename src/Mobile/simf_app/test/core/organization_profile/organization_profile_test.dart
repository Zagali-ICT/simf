import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';

void main() {
  group('OrgSocial.fromJson', () {
    test('reads the API camelCase keys (linkedIn/youTube/tikTok)', () {
      // Regression: the /app/organization-profile API serialises with
      // System.Text.Json camelCase, so the keys arrive as linkedIn/youTube/
      // tikTok. The parser used to read all-lowercase, silently dropping the
      // CP-set LinkedIn/YouTube/TikTok links (2026-06-27).
      final s = OrgSocial.fromJson(<String, dynamic>{
        'x': 'https://x.com/simf',
        'linkedIn': 'https://linkedin.com/company/simf',
        'youTube': 'https://youtube.com/@simf',
        'tikTok': 'https://tiktok.com/@simf',
      });
      expect(s.x, 'https://x.com/simf');
      expect(s.linkedin, 'https://linkedin.com/company/simf');
      expect(s.youtube, 'https://youtube.com/@simf');
      expect(s.tiktok, 'https://tiktok.com/@simf');
    });
  });

  group('OrgProfile.fromJson', () {
    test('reads the nested social with camelCase keys', () {
      final p = OrgProfile.fromJson(<String, dynamic>{
        'name': 'Forum',
        'nameArabic': 'الملتقى',
        'social': <String, dynamic>{'youTube': 'https://youtube.com/@simf'},
      });
      expect(p.social.youtube, 'https://youtube.com/@simf');
    });
  });
}
