import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/features/splash/splash_controller.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake auth controller that boots straight into a fixed state, so the
/// splash decision can be exercised without the real cold-start restore.
class _FakeAuthController extends AuthController {
  _FakeAuthController(this._fixed);

  final AuthState _fixed;

  @override
  AuthState build() => _fixed;
}

class _StubUpdateChecker implements AppUpdateChecker {
  _StubUpdateChecker(this._status);

  final AppUpdateStatus _status;

  @override
  Future<AppUpdateStatus> check() async => _status;

  @override
  Future<void> openStoreListing() async {}
}

/// In-memory [SimfPrefsStorage] so the test needs no platform channel.
class _FakePrefs implements SimfPrefsStorage {
  _FakePrefs([Map<String, Object>? seed]) {
    if (seed != null) {
      _store.addAll(seed);
    }
  }

  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final value = _store[key];
    return value is String ? value : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) {
    final value = _store[key];
    return value is bool ? value : null;
  }

  @override
  Future<bool> setBool(String key, bool value) async {
    _store[key] = value;
    return true;
  }

  @override
  double? getDouble(String key) {
    final value = _store[key];
    return value is double ? value : null;
  }

  @override
  Future<bool> setDouble(String key, double value) async {
    _store[key] = value;
    return true;
  }

  @override
  int? getInt(String key) {
    final value = _store[key];
    return value is int ? value : null;
  }

  @override
  Future<bool> setInt(String key, int value) async {
    _store[key] = value;
    return true;
  }

  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

Session _signedInSession({bool profileComplete = true}) => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(hours: 1)),
      user: CurrentUser(
        id: 'u1',
        email: 'visitor@example.sa',
        displayName: 'Visitor',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('ar'),
        registrationStatus: RegistrationStatus.approved,
        profileComplete: profileComplete,
      ),
    );

Future<SplashState> _resolve(ProviderContainer container) async {
  final completer = Completer<SplashState>();
  final sub = container.listen<SplashState>(
    splashControllerProvider,
    (_, next) {
      if (next is! SplashLoading && !completer.isCompleted) {
        completer.complete(next);
      }
    },
    fireImmediately: true,
  );
  try {
    return await completer.future.timeout(const Duration(seconds: 5));
  } finally {
    sub.close();
  }
}

ProviderContainer _container({
  required AuthState auth,
  required AppUpdateStatus update,
  Map<String, Object> prefs = const <String, Object>{},
}) {
  return ProviderContainer(
    overrides: <Override>[
      minSplashDurationProvider.overrideWithValue(Duration.zero),
      appUpdateCheckerProvider.overrideWithValue(_StubUpdateChecker(update)),
      simfPrefsStorageProvider.overrideWithValue(_FakePrefs(prefs)),
      authControllerProvider.overrideWith(() => _FakeAuthController(auth)),
    ],
  );
}

void main() {
  group('SplashController route-out (Page_001 Logic L-2/L-5)', () {
    test('a forced update short-circuits to SplashUpdateRequired', () async {
      final container = _container(
        auth: const AuthStateSignedOut(),
        update: AppUpdateStatus.forced,
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect(state, isA<SplashUpdateRequired>());
    });

    test('a signed-out first run routes to onboarding', () async {
      final container = _container(
        auth: const AuthStateSignedOut(),
        update: AppUpdateStatus.upToDate,
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect(state, isA<SplashReady>());
      expect((state as SplashReady).routeName, equals(RouteNames.onboarding));
    });

    test('a signed-out returning user routes to sign-in', () async {
      final container = _container(
        auth: const AuthStateSignedOut(),
        update: AppUpdateStatus.upToDate,
        prefs: <String, Object>{StorageKeys.onboardingCompleted: true},
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect((state as SplashReady).routeName, equals(RouteNames.signIn));
    });

    test('a signed-in user with no saved route routes to home', () async {
      final container = _container(
        auth: AuthStateSignedIn(_signedInSession()),
        update: AppUpdateStatus.upToDate,
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect((state as SplashReady).routeName, equals(RouteNames.home));
      expect(state.location, isNull);
    });

    test('a signed-in user resumes the last saved content route', () async {
      final container = _container(
        auth: AuthStateSignedIn(_signedInSession()),
        update: AppUpdateStatus.upToDate,
        prefs: <String, Object>{StorageKeys.lastRoute: '/sessions'},
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect((state as SplashReady).location, equals('/sessions'));
    });

    test('a non-resumable saved route is ignored in favour of home', () async {
      final container = _container(
        auth: AuthStateSignedIn(_signedInSession()),
        update: AppUpdateStatus.upToDate,
        prefs: <String, Object>{StorageKeys.lastRoute: '/sign-up/otp'},
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect((state as SplashReady).routeName, equals(RouteNames.home));
      expect(state.location, isNull);
    });

    test(
        'a signed-in user with an incomplete profile is gated to the '
        'profile form, even over a saved route (D-374)', () async {
      final container = _container(
        auth: AuthStateSignedIn(_signedInSession(profileComplete: false)),
        update: AppUpdateStatus.upToDate,
        prefs: <String, Object>{StorageKeys.lastRoute: '/sessions'},
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect(
        (state as SplashReady).routeName,
        equals(RouteNames.signUpVisitor),
      );
      expect(state.location, isNull);
    });

    test('an optional update flags the soft prompt', () async {
      final container = _container(
        auth: const AuthStateSignedOut(),
        update: AppUpdateStatus.optional,
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect((state as SplashReady).softUpdate, isTrue);
    });

    test('an awaiting-OTP state routes to verify-OTP', () async {
      final container = _container(
        auth: const AuthStateAwaitingOtp('otp-token'),
        update: AppUpdateStatus.upToDate,
      );
      addTearDown(container.dispose);

      final state = await _resolve(container);
      expect((state as SplashReady).routeName, equals(RouteNames.verifyOtp));
    });
  });
}
