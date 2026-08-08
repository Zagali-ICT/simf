import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/net/asset_urls.dart';

void main() {
  const String base = 'https://api.example.com/api/v1';

  group('AssetUrls', () {
    test('builds the D-357 asset route', () {
      expect(
        AssetUrls.image(base, AssetKind.speakerPhoto, 'abc'),
        'https://api.example.com/api/v1/app/assets/SpeakerPhoto/abc/image',
      );
    });

    // The wire names are the shipped contract (D-219). A Dart-side rename must
    // never change the path, so every segment is pinned here: if one of these
    // fails, an installed build has stopped loading that image.
    test('pins every wire name against the shipped contract', () {
      expect(
        <AssetKind, String>{
          for (final AssetKind kind in AssetKind.values) kind: kind.wireName,
        },
        <AssetKind, String>{
          AssetKind.banner: 'Banner',
          AssetKind.boothLogo: 'BoothLogo',
          AssetKind.companyLogo: 'CompanyLogo',
          AssetKind.exhibitorLogo: 'ExhibitorLogo',
          AssetKind.mediaPartnerLogo: 'MediaPartnerLogo',
          AssetKind.newsImage: 'NewsImage',
          AssetKind.programmeDayImage: 'ProgrammeDayImage',
          AssetKind.speakerPhoto: 'SpeakerPhoto',
          AssetKind.sponsorLogo: 'SponsorLogo',
        },
      );
    });

    test('does not re-append the api segment already in baseUrl', () {
      expect(
        AssetUrls.image(base, AssetKind.newsImage, '7'),
        isNot(contains('/api/v1/api/v1')),
      );
    });
  });
}
