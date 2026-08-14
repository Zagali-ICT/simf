import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// D-731 — the Bi-Meeting eligibility provider (D-760) sits behind a profile
/// GET on the shared per-IP "auth" rate-limit bucket, and is watched by the
/// Guest+ speaker-profile browse screen. These tests lock the two properties
/// that keep browsing from draining the sign-in/OTP budget: a guest makes NO
/// profile call, and a signed-in session fetches the flags exactly once. The
/// mapping tests in between only check which flag lands where.

/// Records every profile fetch so a test can assert the network was (not) hit.
class _RecordingProfileRepo implements ProfileRepository {
  _RecordingProfileRepo({this.speaker = false, this.delegation = false});

  final bool speaker;
  final bool delegation;
  int getMyProfileCalls = 0;

  @override
  Future<UserProfileResponse> getMyProfile() async {
    getMyProfileCalls++;
    return UserProfileResponse(
      interestIds: const <String>[],
      arabicName: 'x',
      englishName: 'x',
      nationalityCode: 'SA',
      placeOfBirth: 'Riyadh',
      isSaudi: true,
      gender: AppGender.male,
      hasIdImage: false,
      hasAvatar: false,
      allowsSpeakerMeeting: speaker,
      allowsDelegationMeeting: delegation,
    );
  }

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

CurrentUser _user() => CurrentUser(
      id: 'u1',
      email: 'v@x.sa',
      displayName: 'Visitor One',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('en'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(),
    );

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _Guest extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

ProviderContainer _container({
  required AuthController controller,
  required ProfileRepository repo,
}) {
  final container = ProviderContainer(
    overrides: <Override>[
      authControllerProvider.overrideWith(() => controller),
      profileRepositoryProvider.overrideWithValue(repo),
    ],
  );
  addTearDown(container.dispose);
  return container;
}

void main() {
  group('currentUserMeetingAccessProvider (D-731 — auth-bucket guard)', () {
    test('a guest resolves none WITHOUT calling the profile endpoint',
        () async {
      // Both flags true so the guard, not the repo value, returns none.
      final repo = _RecordingProfileRepo(speaker: true, delegation: true);
      final container = _container(controller: _Guest(), repo: repo);

      final access =
          await container.read(currentUserMeetingAccessProvider.future);

      expect(access.speaker, isFalse);
      expect(access.delegation, isFalse);
      expect(access.any, isFalse);
      expect(
        repo.getMyProfileCalls,
        0,
        reason: 'a guest is never eligible — no auth-bucket call',
      );
    });

    // The two flags are independent, so walk the whole truth table: either one
    // alone opens the Bi-Meeting feed ([MeetingAccess.any]), and neither flag
    // may leak into the other.
    void mapsThrough({required bool speaker, required bool delegation}) {
      test('signed-in maps speaker=$speaker delegation=$delegation', () async {
        final repo =
            _RecordingProfileRepo(speaker: speaker, delegation: delegation);
        final container = _container(controller: _SignedIn(), repo: repo);

        final access =
            await container.read(currentUserMeetingAccessProvider.future);

        expect(access.speaker, speaker);
        expect(access.delegation, delegation);
        expect(access.any, speaker || delegation);
      });
    }

    mapsThrough(speaker: true, delegation: true);
    mapsThrough(speaker: true, delegation: false);
    mapsThrough(speaker: false, delegation: true);
    mapsThrough(speaker: false, delegation: false);

    test('the flags are cached across reads under a stable session', () async {
      final repo = _RecordingProfileRepo(speaker: true);
      final container = _container(controller: _SignedIn(), repo: repo);

      // Three reads with NO auth transition between them = the browse case
      // (re-opening speaker profiles). Not autoDispose → one fetch, cached.
      await container.read(currentUserMeetingAccessProvider.future);
      await container.read(currentUserMeetingAccessProvider.future);
      await container.read(currentUserMeetingAccessProvider.future);

      expect(
        repo.getMyProfileCalls,
        1,
        reason: 'not autoDispose → re-opening speaker profiles under a stable '
            'session never re-hits the auth bucket (it re-fetches only on an '
            'auth transition)',
      );
    });
  });
}
