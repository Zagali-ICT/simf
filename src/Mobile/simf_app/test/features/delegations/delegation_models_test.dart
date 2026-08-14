import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';

DelegationItem _item({
  String code = 'US',
  String name = 'United States',
  String nameAr = 'الولايات المتحدة',
  int members = 1,
  String? head,
  String? headAr,
  String? title,
  DateTime? arrival,
  DateTime? departure,
}) =>
    DelegationItem(
      countryId: 1,
      countryCode: code,
      countryName: name,
      countryNameArabic: nameAr,
      memberCount: members,
      headName: head,
      headNameArabic: headAr,
      headTitle: title,
      arrivalDate: arrival,
      departureDate: departure,
    );

void main() {
  group('Delegations.fromData', () {
    test('parses the stats and the item fields', () {
      final data = Delegations.fromData(const <String, dynamic>{
        'countryCount': 2,
        'totalParticipants': 19,
        'items': <dynamic>[
          <String, dynamic>{
            'countryId': 682,
            'countryCode': 'SA',
            'countryName': 'Saudi Arabia',
            'countryNameArabic': 'المملكة العربية السعودية',
            'memberCount': 12,
            'headName': 'Prince Abdulaziz',
            'headNameArabic': 'سمو الأمير عبدالعزيز',
            'headTitle': 'Deputy Minister of Defense',
            'headTitleArabic': 'نائب وزير الدفاع',
            'arrivalDate': '2026-01-12',
            'departureDate': '2026-01-15',
          },
        ],
      });

      expect(data.countryCount, 2);
      expect(data.totalParticipants, 19);
      expect(data.items, hasLength(1));
      final item = data.items.first;
      expect(item.countryId, 682);
      expect(item.memberCount, 12);
      expect(item.headName, 'Prince Abdulaziz');
      expect(item.headTitle, 'Deputy Minister of Defense');
      expect(item.headTitleArabic, 'نائب وزير الدفاع');
      // Owner 2026-07-19 — the head-of-delegation title localizes AR/EN.
      expect(item.localizedHeadTitle(true), 'نائب وزير الدفاع');
      expect(item.localizedHeadTitle(false), 'Deputy Minister of Defense');
      expect(item.arrivalDate, DateTime(2026, 1, 12));
      expect(item.departureDate, DateTime(2026, 1, 15));
      expect(item.hasHead, isTrue);
      expect(item.hasDateRange, isTrue);
    });

    test('defaults gracefully on a null / malformed payload', () {
      final data = Delegations.fromData(null);
      expect(data.items, isEmpty);
      expect(data.countryCount, 0);
      expect(data.totalParticipants, 0);
    });

    test('countryCount falls back to the item count when absent', () {
      final data = Delegations.fromData(const <String, dynamic>{
        'items': <dynamic>[
          <String, dynamic>{'countryId': 1, 'countryCode': 'SA'},
        ],
      });
      expect(data.countryCount, 1);
    });
  });

  group('DelegationItem', () {
    test('flagEmoji maps an alpha-2 code to its regional-indicator pair', () {
      // SA → 🇸🇦 = U+1F1F8 U+1F1E6.
      expect(_item(code: 'SA').flagEmoji.runes.toList(), <int>[0x1F1F8, 0x1F1E6]);
    });

    test('flagEmoji is empty for a malformed code', () {
      expect(_item(code: 'X').flagEmoji, '');
      expect(_item(code: '12').flagEmoji, '');
    });

    test('headInitial returns the first character of the localized head', () {
      final item = _item(head: 'James', headAr: 'جيمس');
      expect(item.headInitial(false), 'J');
      expect(item.headInitial(true), 'ج');
    });

    test('headInitial is empty when there is no head', () {
      expect(_item().headInitial(false), '');
    });

    test('matches filters by country name, code and head', () {
      final item = _item(head: 'James Mitchell');
      expect(item.matches(''), isTrue);
      expect(item.matches('united'), isTrue);
      expect(item.matches('الولايات'), isTrue);
      expect(item.matches('us'), isTrue); // the country code
      expect(item.matches('mitchell'), isTrue);
      expect(item.matches('france'), isFalse);
    });

    test('hasHead / hasDateRange / localizedHead reflect the unset case', () {
      final item = _item(name: 'France', nameAr: 'فرنسا');
      expect(item.hasHead, isFalse);
      expect(item.hasDateRange, isFalse);
      expect(item.localizedHead(false), isNull);
    });

    test('localized helpers pick per locale', () {
      final item = _item();
      expect(item.localizedCountry(false), 'United States');
      expect(item.localizedCountry(true), 'الولايات المتحدة');
      expect(item.localizedCountrySubtitle(false), 'الولايات المتحدة');
      expect(item.localizedCountrySubtitle(true), 'United States');
    });
  });
}
