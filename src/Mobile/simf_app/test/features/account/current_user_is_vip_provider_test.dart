import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// D-731 — the VIP-flag provider (D-729) sits behind a profile GET on the
/// shared per-IP "auth" rate-limit bucket, and is watched by the Guest+
/// speaker-profile browse screen. These tests lock the two properties that keep
/// browsing from draining the sign-in/OTP budget: a guest makes NO profile
/// call, and a signed-in session fetches the flag exactly once.

/// Records every profile fetch so a test can assert the network was (not) hit.
class _RecordingProfileRepo implements ProfileRepository {
  _RecordingProfileRepo({required this.isVip});

  final bool isVip;
  int getMyProfileCalls = 0;

  @override
  Future<UserProfileResponse> getMyProfile() async {
    getMyProfileCalls++;
    return _profile(isVip: isVip);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

UserProfileResponse _profile({required bool isVip}) => UserProfileResponse(
      interestIds: const <String>[],
      arabicName: 'x',
      englishName: 'x',
      nationalityCode: 'SA',
      placeOfBirth: 'Riyadh',
      isSaudi: true,
      gender: AppGender.male,
      hasIdImage: false,
      hasAvatar: false,
      isVip: isVip,
    );

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
  group('currentUserIsVipProvider (D-731 — auth-bucket guard)', () {
    test('a guest resolves false WITHOUT calling the profile endpoint',
        () async {
      // isVip:true so the guard, not the repo value, is what returns false.
      final repo = _RecordingProfileRepo(isVip: true);
      final container = _container(controller: _Guest(), repo: repo);

      final isVip = await container.read(currentUserIsVipProvider.future);

      expect(isVip, isFalse);
      expect(
        repo.getMyProfileCalls,
        0,
        reason: 'a guest can never be VIP — no auth-bucket call',
      );
    });

    test('a signed-in VIP resolves true with a single profile fetch', () async {
      final repo = _RecordingProfileRepo(isVip: true);
      final container = _container(controller: _SignedIn(), repo: repo);

      final isVip = await container.read(currentUserIsVipProvider.future);

      expect(isVip, isTrue);
      expect(repo.getMyProfileCalls, 1);
    });

    test('a signed-in NON-VIP resolves false', () async {
      final repo = _RecordingProfileRepo(isVip: false);
      final container = _container(controller: _SignedIn(), repo: repo);

      expect(await container.read(currentUserIsVipProvider.future), isFalse);
      expect(repo.getMyProfileCalls, 1);
    });

    test('the flag is cached across reads under a stable session — fetch once',
        () async {
      final repo = _RecordingProfileRepo(isVip: true);
      final container = _container(controller: _SignedIn(), repo: repo);

      // Three reads with NO auth transition between them = the browse case
      // (re-opening speaker profiles). Not autoDispose → one fetch, cached.
      await container.read(currentUserIsVipProvider.future);
      await container.read(currentUserIsVipProvider.future);
      await container.read(currentUserIsVipProvider.future);

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
