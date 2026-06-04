import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/auth/sign_in_screen.dart';
import 'package:simf_app/features/profile/data/profile_models.dart';
import 'package:simf_app/features/profile/data/profile_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

enum _Outcome { success, otp, invalid }

/// A fake controller whose `signIn` transitions to a configured outcome,
/// so the screen's UI → navigation/error glue can be tested in isolation.
class _FakeAuthController extends AuthController {
  _FakeAuthController(this.outcome);

  final _Outcome outcome;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<void> signIn({required String email, required String password}) async {
    switch (outcome) {
      case _Outcome.success:
        state = AuthStateSignedIn(
          Session(
            accessToken: 'A',
            refreshToken: 'R',
            accessTokenExpiresAt: DateTime.now().add(const Duration(hours: 1)),
            user: CurrentUser(
              id: 'u1',
              email: email,
              displayName: 'Visitor',
              appRole: AppRole.visitor,
              preferredLanguage: PreferredLanguage.fromJson('ar'),
              registrationStatus: RegistrationStatus.approved,
            ),
          ),
        );
      case _Outcome.otp:
        state = const AuthStateAwaitingOtp('otp-token');
      case _Outcome.invalid:
        throw const InvalidCredentials(
          ApiFailure(
            code: ApiErrorCodes.authInvalidCredentials,
            message: 'Incorrect email or password.',
            httpStatus: 401,
          ),
        );
    }
  }
}

class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final v = _store[key];
    return v is String ? v : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) => null;
  @override
  Future<bool> setBool(String key, bool value) async => true;
  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

/// Fake profile repo for the post-sign-in completeness probe (Slice 3). Only
/// `getMyProfile` is exercised by the sign-in route; the rest are unused.
class _FakeProfileRepository implements ProfileRepository {
  _FakeProfileRepository({required this.complete});

  final bool complete;

  @override
  Future<UserProfileResponse> getMyProfile() async => UserProfileResponse(
        interestIds: complete ? const <String>['i1'] : const <String>[],
        arabicName: complete ? 'راكان' : '',
        englishName: complete ? 'Rakan' : '',
        nationalityCode: complete ? 'SA' : '',
        placeOfBirth: '',
        isSaudi: true,
        gender: AppGender.unspecified,
        hasIdImage: false,
      );

  @override
  Future<UserProfileResponse> upsertMyProfile(UpsertUserProfileRequest r) =>
      throw UnimplementedError();
  @override
  Future<List<CountryItem>> getCountries() => throw UnimplementedError();
  @override
  Future<List<ProfileTypeItem>> getProfileTypes() => throw UnimplementedError();
  @override
  Future<List<InterestItem>> getInterests() => throw UnimplementedError();
  @override
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) =>
      throw UnimplementedError();
  @override
  Future<bool> uploadIdImage({
    required List<int> bytes,
    required String filename,
  }) =>
      throw UnimplementedError();
}

Future<void> _pump(
  WidgetTester tester,
  _Outcome outcome,
  _FakePrefs prefs, {
  bool profileComplete = true,
}) async {
  final router = GoRouter(
    initialLocation: '/sign-in',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const SignInScreen(),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
      ),
      GoRoute(
        name: RouteNames.verifyOtp,
        path: '/auth/verify-otp',
        builder: (c, s) => const Scaffold(body: Text('OTP-SCREEN')),
      ),
      GoRoute(
        name: RouteNames.forgotPassword,
        path: '/auth/forgot-password',
        builder: (c, s) => const Scaffold(body: Text('FORGOT')),
      ),
      GoRoute(
        name: RouteNames.signUpForm,
        path: '/sign-up',
        builder: (c, s) => const Scaffold(body: Text('SIGN-UP')),
      ),
      GoRoute(
        name: RouteNames.signUpVisitor,
        path: '/sign-up/visitor',
        builder: (c, s) => const Scaffold(body: Text('PROFILE')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfPrefsStorageProvider.overrideWithValue(prefs),
        authControllerProvider.overrideWith(() => _FakeAuthController(outcome)),
        profileRepositoryProvider.overrideWithValue(
          _FakeProfileRepository(complete: profileComplete),
        ),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _enterCreds(WidgetTester tester) async {
  await tester.enterText(find.byType(TextField).at(0), 'visitor@example.sa');
  await tester.enterText(find.byType(TextField).at(1), 'Password1');
  await tester.pump();
}

void main() {
  group('SignInScreen (Page 003)', () {
    testWidgets('successful sign-in with a complete profile routes home and '
        'stores the email', (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
      expect(prefs.getString(StorageKeys.lastEmail), equals('visitor@example.sa'));
    });

    testWidgets('successful sign-in with an incomplete profile routes to the '
        'visitor profile screen (Page_007 auto-route)', (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs, profileComplete: false);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('PROFILE'), findsOneWidget);
      expect(find.text('HOME'), findsNothing);
      // The email is still recorded for the next cold start.
      expect(prefs.getString(StorageKeys.lastEmail), equals('visitor@example.sa'));
    });

    testWidgets('a 2FA account routes to the email-OTP screen', (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.otp, prefs);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('OTP-SCREEN'), findsOneWidget);
    });

    testWidgets('invalid credentials show the error and stay on sign-in',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.invalid, prefs);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('Incorrect email or password.'), findsOneWidget);
      expect(find.text('HOME'), findsNothing);
    });

    testWidgets('the email field is pre-filled from the last sign-in',
        (tester) async {
      final prefs = _FakePrefs();
      await prefs.setString(StorageKeys.lastEmail, 'prefilled@example.sa');
      await _pump(tester, _Outcome.success, prefs);

      expect(find.text('prefilled@example.sa'), findsOneWidget);
    });
  });
}
