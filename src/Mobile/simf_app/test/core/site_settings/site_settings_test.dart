import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';

void main() {
  group('SiteSocialLinks.fromJson', () {
    test('reads the API camelCase keys, incl. linkedIn/youTube/tikTok', () {
      // Regression: the API serialises with System.Text.Json camelCase, so the
      // multi-word keys arrive as linkedIn/youTube/tikTok. The parser used to
      // read all-lowercase keys, silently dropping the CP-set LinkedIn/YouTube/
      // TikTok URLs (the "social click does nothing" report, 2026-06-27).
      final social = SiteSocialLinks.fromJson(<String, dynamic>{
        'facebook': 'https://facebook.com/simf',
        'x': 'https://x.com/simf',
        'instagram': 'https://instagram.com/simf',
        'linkedIn': 'https://linkedin.com/company/simf',
        'youTube': 'https://youtube.com/@simf',
        'tikTok': 'https://tiktok.com/@simf',
        'snapchat': 'https://snapchat.com/add/simf',
      });

      expect(social.facebook, 'https://facebook.com/simf');
      expect(social.x, 'https://x.com/simf');
      expect(social.instagram, 'https://instagram.com/simf');
      expect(social.linkedin, 'https://linkedin.com/company/simf');
      expect(social.youtube, 'https://youtube.com/@simf');
      expect(social.tiktok, 'https://tiktok.com/@simf');
      expect(social.snapchat, 'https://snapchat.com/add/simf');
    });

    test('missing keys resolve to null (the control stays inert)', () {
      final social = SiteSocialLinks.fromJson(const <String, dynamic>{});
      expect(social.x, isNull);
      expect(social.linkedin, isNull);
      expect(social.youtube, isNull);
      expect(social.tiktok, isNull);
    });
  });

  group('SiteSettings.fromJson', () {
    test('reads the bilingual welcome message + nested social', () {
      final settings = SiteSettings.fromJson(<String, dynamic>{
        'registrationSuccessMessageAr': 'مرحبا',
        'registrationSuccessMessageEn': 'Welcome',
        'social': <String, dynamic>{'youTube': 'https://youtube.com/@simf'},
      });

      expect(settings.messageFor('ar'), 'مرحبا');
      expect(settings.messageFor('en'), 'Welcome');
      expect(settings.social.youtube, 'https://youtube.com/@simf');
    });
  });
}
