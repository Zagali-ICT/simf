import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';

// Frozen wire fixtures for the cached Organization profile (D-495): there is
// no `toJson` — the raw server map is re-read through `OrgProfile.fromJson`,
// so the tolerant decoder IS the whole contract.

/// Every field carrying a value the decoder cannot produce by accident.
const String _sentinelJson = '''
{
  "name": "WIRE:name",
  "nameArabic": "WIRE:nameArabic",
  "title": "WIRE:title",
  "titleArabic": "WIRE:titleArabic",
  "currentYear": 4242,
  "status": "WIRE:status",
  "slogan": "WIRE:slogan",
  "sloganArabic": "WIRE:sloganArabic",
  "bio": "WIRE:bio",
  "bioArabic": "WIRE:bioArabic",
  "locationText": "WIRE:locationText",
  "locationTextArabic": "WIRE:locationTextArabic",
  "contactPhone": "WIRE:contactPhone",
  "contactEmail": "WIRE:contactEmail",
  "contactWebsite": "WIRE:contactWebsite",
  "liveStreamUrl": "WIRE:liveStreamUrl",
  "backgroundVideoUrl": "WIRE:backgroundVideoUrl",
  "logoUrl": "WIRE:logoUrl",
  "version": "WIRE:version",
  "eventStartDate": "2031-04-17T00:00:00+03:00",
  "eventEndDate": "2031-04-19T00:00:00+03:00",
  "social": {
    "facebook": "WIRE:facebook",
    "x": "WIRE:x",
    "instagram": "WIRE:instagram",
    "linkedIn": "WIRE:linkedIn",
    "youTube": "WIRE:youTube",
    "tikTok": "WIRE:tikTok",
    "snapchat": "WIRE:snapchat"
  },
  "aboutItems": [
    {
      "title": "WIRE:aboutTitle",
      "titleArabic": "WIRE:aboutTitleArabic",
      "text": "WIRE:aboutText",
      "textArabic": "WIRE:aboutTextArabic"
    }
  ],
  "details": [
    {
      "name": "WIRE:detailName",
      "nameArabic": "WIRE:detailNameArabic",
      "value": "WIRE:detailValue",
      "valueArabic": "WIRE:detailValueArabic"
    }
  ]
}
''';

/// Every key present, every value null. Pins the fallbacks themselves.
const String _allNullsJson = '''
{
  "name": null,
  "nameArabic": null,
  "title": null,
  "titleArabic": null,
  "currentYear": null,
  "status": null,
  "slogan": null,
  "sloganArabic": null,
  "bio": null,
  "bioArabic": null,
  "locationText": null,
  "locationTextArabic": null,
  "contactPhone": null,
  "contactEmail": null,
  "contactWebsite": null,
  "liveStreamUrl": null,
  "backgroundVideoUrl": null,
  "logoUrl": null,
  "version": null,
  "eventStartDate": null,
  "eventEndDate": null,
  "social": null,
  "aboutItems": null,
  "details": null
}
''';

const String _allNullsSocialJson = '''
{
  "facebook": null,
  "x": null,
  "instagram": null,
  "linkedIn": null,
  "youTube": null,
  "tikTok": null,
  "snapchat": null
}
''';

Map<String, dynamic> _decode(String json) =>
    jsonDecode(json) as Map<String, dynamic>;

void _expectEverythingDefaulted(OrgProfile profile) {
  expect(profile.name, '');
  expect(profile.nameArabic, '');
  expect(profile.title, '');
  expect(profile.titleArabic, '');
  expect(profile.currentYear, 0);
  expect(profile.status, '');
  expect(profile.slogan, isNull);
  expect(profile.sloganArabic, isNull);
  expect(profile.bio, isNull);
  expect(profile.bioArabic, isNull);
  expect(profile.locationText, isNull);
  expect(profile.locationTextArabic, isNull);
  expect(profile.contactPhone, isNull);
  expect(profile.contactEmail, isNull);
  expect(profile.contactWebsite, isNull);
  expect(profile.liveStreamUrl, isNull);
  expect(profile.backgroundVideoUrl, isNull);
  expect(profile.logoUrl, isNull);
  expect(profile.version, isNull);
  expect(profile.eventStartDate, isNull);
  expect(profile.eventEndDate, isNull);
  expect(profile.aboutItems, isEmpty);
  expect(profile.details, isEmpty);
  // An absent social block still yields an OrgSocial, so the controls render
  // inert rather than the page failing to build.
  expect(profile.social.facebook, isNull);
  expect(profile.social.linkedin, isNull);
  expect(profile.social.youtube, isNull);
  expect(profile.social.tiktok, isNull);
  expect(profile.eventDateRange(isArabic: true), isNull);
}

void main() {
  group('OrgProfile — frozen sentinel fixture', () {
    test('every scalar key decodes to its sentinel, not to a fallback', () {
      final profile = OrgProfile.fromJson(_decode(_sentinelJson));

      expect(profile.name, 'WIRE:name');
      expect(profile.nameArabic, 'WIRE:nameArabic');
      expect(profile.title, 'WIRE:title');
      expect(profile.titleArabic, 'WIRE:titleArabic');
      expect(profile.currentYear, 4242);
      expect(profile.status, 'WIRE:status');
      expect(profile.slogan, 'WIRE:slogan');
      expect(profile.sloganArabic, 'WIRE:sloganArabic');
      expect(profile.bio, 'WIRE:bio');
      expect(profile.bioArabic, 'WIRE:bioArabic');
      expect(profile.locationText, 'WIRE:locationText');
      expect(profile.locationTextArabic, 'WIRE:locationTextArabic');
      expect(profile.contactPhone, 'WIRE:contactPhone');
      expect(profile.contactEmail, 'WIRE:contactEmail');
      expect(profile.contactWebsite, 'WIRE:contactWebsite');
      expect(profile.liveStreamUrl, 'WIRE:liveStreamUrl');
      expect(profile.backgroundVideoUrl, 'WIRE:backgroundVideoUrl');
      expect(profile.logoUrl, 'WIRE:logoUrl');
      expect(profile.version, 'WIRE:version');
    });

    test('the event dates decode to the WRITTEN calendar day', () {
      final profile = OrgProfile.fromJson(_decode(_sentinelJson));

      // Only the YYYY-MM-DD head is read, so the +03:00 offset must not shift
      // the day in any timezone.
      expect(profile.eventStartDate, DateTime(2031, 4, 17));
      expect(profile.eventEndDate, DateTime(2031, 4, 19));
    });

    test('the camelCase social keys decode — linkedIn / youTube / tikTok', () {
      final social = OrgProfile.fromJson(_decode(_sentinelJson)).social;

      expect(social.facebook, 'WIRE:facebook');
      expect(social.x, 'WIRE:x');
      expect(social.instagram, 'WIRE:instagram');
      // The fields are lowercase, the wire keys camelCase — an all-lowercase
      // key decodes to null without throwing, and has done once already.
      expect(social.linkedin, 'WIRE:linkedIn');
      expect(social.youtube, 'WIRE:youTube');
      expect(social.tiktok, 'WIRE:tikTok');
      expect(social.snapchat, 'WIRE:snapchat');
    });

    test('the nested aboutItems entry decodes every key', () {
      final item = OrgProfile.fromJson(_decode(_sentinelJson))
          .aboutItems
          .single;

      expect(item.title, 'WIRE:aboutTitle');
      expect(item.titleArabic, 'WIRE:aboutTitleArabic');
      expect(item.text, 'WIRE:aboutText');
      expect(item.textArabic, 'WIRE:aboutTextArabic');
    });

    test('the nested details entry decodes every key', () {
      final detail = OrgProfile.fromJson(_decode(_sentinelJson)).details.single;

      expect(detail.name, 'WIRE:detailName');
      expect(detail.nameArabic, 'WIRE:detailNameArabic');
      expect(detail.value, 'WIRE:detailValue');
      expect(detail.valueArabic, 'WIRE:detailValueArabic');
    });
  });

  group('OrgProfile — fallbacks', () {
    test('an all-nulls object defaults every field', () {
      _expectEverythingDefaulted(OrgProfile.fromJson(_decode(_allNullsJson)));
    });

    test('an empty object defaults every field identically', () {
      _expectEverythingDefaulted(OrgProfile.fromJson(_decode('{}')));
    });

    test('an all-nulls social block leaves every link null', () {
      final social = OrgSocial.fromJson(_decode(_allNullsSocialJson));

      expect(social.facebook, isNull);
      expect(social.x, isNull);
      expect(social.instagram, isNull);
      expect(social.linkedin, isNull);
      expect(social.youtube, isNull);
      expect(social.tiktok, isNull);
      expect(social.snapchat, isNull);
    });

    test('an all-nulls about item and detail default their fields', () {
      final item = OrgAboutItem.fromJson(
        _decode('{"title": null, "titleArabic": null, "text": null,'
            ' "textArabic": null}'),
      );
      final detail = OrgDetail.fromJson(
        _decode('{"name": null, "nameArabic": null, "value": null,'
            ' "valueArabic": null}'),
      );

      expect(item.title, '');
      expect(item.titleArabic, '');
      expect(item.text, '');
      expect(item.textArabic, '');
      expect(detail.name, '');
      expect(detail.nameArabic, '');
      expect(detail.value, '');
      expect(detail.valueArabic, isNull);
      // A null Arabic value falls back to the neutral value, not to ''.
      expect(detail.valueFor(isArabic: true), '');
    });

    test('a malformed date is null rather than a throw', () {
      final profile = OrgProfile.fromJson(
        _decode('{"eventStartDate": "2031", "eventEndDate": "not-a-date!"}'),
      );

      expect(profile.eventStartDate, isNull);
      expect(profile.eventEndDate, isNull);
    });
  });
}
