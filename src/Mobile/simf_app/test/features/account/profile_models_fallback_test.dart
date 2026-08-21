import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_models.dart';

/// `UserProfileResponse.fromJson` is tolerant by design, so a payload that
/// OMITS a key decodes to the fallback instead of throwing. For the flags
/// below the fallback is not a cosmetic default — it is the answer to "may
/// this account do X", so a wrong one silently opens a gate and no test that
/// only feeds a fully-populated fixture can see it.
///
///  * `allowsSpeakerMeeting` / `allowsDelegationMeeting` are the Bi-Meeting
///    entitlements (D-760, replacing the D-729 VIP-tier gate). They are
///    ADMIN-ASSIGNED per user, so the absence of a grant is a denial: the
///    fallback must be false, or an older server — or any response that drops
///    the key — hands every account the meeting CTA it was never granted.
///  * `isSaudi` picks which mobile number the profile reads and writes
///    (`saudiMobile` vs `internationalMobile`) and which identity document the
///    forms demand, so a wrong fallback misroutes real data rather than a
///    label.
///
/// Each flag gets three cases: a SENTINEL that the fallback cannot produce
/// (proving the key is read at all), the key ABSENT, and the key present but
/// NULL. The last two are the half a sentinel fixture cannot see.
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
      // The sibling grant that WAS sent must still land, so this is a
      // per-flag default and not a blanket "deny everything".
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
      // The consequence, not just the flag: the re-save writes the edited
      // number to the field the nationality selects, so a wrong default sends
      // a foreign number up as a Saudi one.
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
