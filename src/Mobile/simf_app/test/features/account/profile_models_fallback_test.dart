import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_models.dart';

/// `UserProfileResponse.fromJson` is tolerant, so a missing key decodes to its
/// fallback: the admin-assigned Bi-Meeting entitlements (D-760) must fall back
/// to DENIED, and `isSaudi` to false, since it routes the mobile number.
void main() {
  group('UserProfileResponse — meeting entitlements default CLOSED', () {
    test('a granted payload decodes both flags true, not to a fallback', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'allowsSpeakerMeeting': true,
        'allowsDelegationMeeting': true,
      });

      expect(profile.allowsSpeakerMeeting, isTrue);
      expect(profile.allowsDelegationMeeting, isTrue);
    });

    test('an ABSENT allowsSpeakerMeeting denies the speaker meeting', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'arabicName': 'راكان',
        'allowsDelegationMeeting': true,
      });

      expect(profile.allowsSpeakerMeeting, isFalse);
      // Per-flag, not a blanket "deny everything".
      expect(profile.allowsDelegationMeeting, isTrue);
    });

    test('an ABSENT allowsDelegationMeeting denies the delegation meeting', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'allowsSpeakerMeeting': true,
      });

      expect(profile.allowsDelegationMeeting, isFalse);
      expect(profile.allowsSpeakerMeeting, isTrue);
    });

    test('an explicitly NULL entitlement denies it too', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'allowsSpeakerMeeting': null,
        'allowsDelegationMeeting': null,
      });

      expect(profile.allowsSpeakerMeeting, isFalse);
      expect(profile.allowsDelegationMeeting, isFalse);
    });

    test('an empty payload grants nothing at all', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{});

      expect(profile.allowsSpeakerMeeting, isFalse);
      expect(profile.allowsDelegationMeeting, isFalse);
      expect(profile.isVip, isFalse);
    });
  });

  group('UserProfileResponse — isSaudi defaults to NOT Saudi', () {
    test('a Saudi payload decodes true, not to a fallback', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'isSaudi': true,
      });

      expect(profile.isSaudi, isTrue);
    });

    test('an ABSENT isSaudi routes the mobile to internationalMobile', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'nationalityCode': 'EG',
      });

      expect(profile.isSaudi, isFalse);
      // The flag picks the field the re-save writes the number to.
      final request = profile.toUpsertRequest(mobile: '0512345678');
      expect(request.internationalMobile, '0512345678');
      expect(request.saudiMobile, isNull);
      expect(request.isSaudi, isFalse);
    });

    test('an explicitly NULL isSaudi is NOT Saudi', () {
      final profile = UserProfileResponse.fromJson(const <String, dynamic>{
        'isSaudi': null,
      });

      expect(profile.isSaudi, isFalse);
    });
  });
}
