import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';

void main() {
  group('VenueMapNodeKind.fromJson', () {
    test('decodes the integer wire value', () {
      expect(VenueMapNodeKind.fromJson(0), VenueMapNodeKind.hall);
      expect(VenueMapNodeKind.fromJson(2), VenueMapNodeKind.booth);
    });

    test('also tolerates the string name', () {
      expect(VenueMapNodeKind.fromJson('Hall'), VenueMapNodeKind.hall);
      expect(VenueMapNodeKind.fromJson('Booth'), VenueMapNodeKind.booth);
    });

    test('falls back to pointOfInterest on an unknown value', () {
      expect(VenueMapNodeKind.fromJson(99), VenueMapNodeKind.pointOfInterest);
      expect(VenueMapNodeKind.fromJson('Mystery'), VenueMapNodeKind.pointOfInterest);
      expect(VenueMapNodeKind.fromJson(null), VenueMapNodeKind.pointOfInterest);
    });
  });

  group('VenueMapNode.fromJson', () {
    test('parses a booth node with its position and booth link', () {
      final node = VenueMapNode.fromJson(const <String, dynamic>{
        'id': 'n1',
        'label': 'Booth A-12',
        'labelArabic': 'جناح A-12',
        'kind': 2,
        'x': 10.5,
        'y': 20.0,
        'hallId': null,
        'boothId': 'b1',
      });

      expect(node.isBooth, isTrue);
      expect(node.x, 10.5);
      expect(node.y, 20.0);
      expect(node.boothId, 'b1');
      expect(node.localizedLabel(true), 'جناح A-12');
      expect(node.localizedLabel(false), 'Booth A-12');
    });
  });

  group('BoothSummary / BoothDetail.fromJson', () {
    test('binds the real wire field names (name/nameArabic/exhibitorName/…)', () {
      final booth = BoothSummary.fromJson(const <String, dynamic>{
        'id': 'b1',
        'code': 'A-12',
        'name': 'SAMI',
        'nameArabic': 'سامي',
        'exhibitorName': 'SAMI Co',
        'exhibitorNameArabic': 'شركة سامي',
        'sector': 'Defense',
        'sectorArabic': 'دفاع',
        'hallId': 'h1',
      });

      expect(booth.code, 'A-12');
      expect(booth.localizedName(false), 'SAMI');
      expect(booth.localizedName(true), 'سامي');
      expect(booth.localizedExhibitor(false), 'SAMI Co');
      expect(booth.localizedSector(true), 'دفاع');
    });

    test('detail adds the localized description', () {
      final detail = BoothDetail.fromJson(const <String, dynamic>{
        'id': 'b1',
        'code': 'A-12',
        'name': 'SAMI',
        'nameArabic': 'سامي',
        'description': 'World-class maritime systems.',
        'descriptionArabic': 'أنظمة بحرية عالمية.',
      });

      expect(detail.localizedDescription(false), 'World-class maritime systems.');
      expect(detail.localizedDescription(true), 'أنظمة بحرية عالمية.');
    });

    test('missing nullable fields resolve to null, not empty strings', () {
      final booth = BoothSummary.fromJson(const <String, dynamic>{
        'id': 'b1',
        'code': 'A-1',
        'name': 'X',
        'nameArabic': 'س',
      });
      expect(booth.localizedExhibitor(false), isNull);
      expect(booth.localizedSector(false), isNull);
    });
  });
}
