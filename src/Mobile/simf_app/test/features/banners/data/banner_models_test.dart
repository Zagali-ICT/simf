import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/banners/data/banner_models.dart';

void main() {
  group('PublicBannerItem (#43)', () {
    test('fromJson decodes every field', () {
      final item = PublicBannerItem.fromJson(<String, dynamic>{
        'id': 'b1',
        'title': 'Welcome',
        'titleArabic': 'مرحبا',
        'body': 'Body',
        'bodyArabic': 'النص',
        'imageUrl': 'https://cdn.example/x.jpg',
        'linkUrl': 'https://example.sa',
        'displayOrder': 3,
      });
      expect(item.id, 'b1');
      expect(item.titleFor(true), 'مرحبا');
      expect(item.titleFor(false), 'Welcome');
      expect(item.imageUrl, 'https://cdn.example/x.jpg');
      expect(item.linkUrl, 'https://example.sa');
      expect(item.displayOrder, 3);
    });

    test('assetImageUrl builds the by-id serve path (no /api/v1 re-append)', () {
      const item = PublicBannerItem(
        id: 'b1',
        title: '',
        titleArabic: '',
        body: '',
        bodyArabic: '',
        displayOrder: 0,
      );
      expect(
        item.assetImageUrl('http://x/api/v1'),
        'http://x/api/v1/app/assets/Banner/b1/image',
      );
    });

    test('listFromData reads the {items:[…]} envelope; empty on absent/null', () {
      final list = PublicBannerItem.listFromData(<String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{'id': 'a', 'displayOrder': 0},
          <String, dynamic>{'id': 'b', 'displayOrder': 1},
        ],
      });
      expect(list.map((b) => b.id).toList(), <String>['a', 'b']);
      expect(PublicBannerItem.listFromData(const <String, dynamic>{}), isEmpty);
      expect(PublicBannerItem.listFromData(null), isEmpty);
    });
  });
}
