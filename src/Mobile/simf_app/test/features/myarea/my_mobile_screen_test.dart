import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/myarea/my_mobile_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The screen now reads the SHARED `myProfileProvider`, which reads the auth
/// state and the data config — so the harness has to supply both. It used to
/// read the repository directly and needed neither.
const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: CurrentUser(
            id: 'u1',
            email: 'v@example.sa',
            displayName: 'Raed',
            appRole: AppRole.visitor,
            preferredLanguage: PreferredLanguage.fromJson('en'),
            registrationStatus: RegistrationStatus.approved,
          ),
        ),
      );
}

/// Cover for the owner's 2026-07-26 request — "Add / Edit phone number in my
/// profile — NO VERIFY, ONLY VALIDATE".
///
/// The screen re-POSTs the FULL loaded profile with only the mobile replaced,
/// so the assertions are: the stored number pre-fills, an invalid shape is
/// rejected client-side (the same C4 / D-371 rule the server enforces), a valid
/// save carries every other field back untouched, and there is no OTP step.
class _FakeProfileRepository implements ProfileRepository {
  _FakeProfileRepository({required this.myProfile});

  UserProfileResponse myProfile;
  bool throwOnLoad = false;
  bool throwOnSave = false;
  UpsertUserProfileRequest? upserted;

  @override
  Future<UserProfileResponse> getMyProfile() async {
    if (throwOnLoad) {
      throw const ApiFailure(code: 'X', message: 'load-boom');
    }
    return myProfile;
  }

  @override
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) async {
    if (throwOnSave) {
      throw const ApiFailure(code: 'X', message: 'save-boom');
    }
    upserted = request;
    return myProfile;
  }

  @override
  Future<bool> uploadIdImage({
    required List<int> bytes,
    required String filename,
  }) =>
      throw UnimplementedError();
  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) =>
      throw UnimplementedError();
  @override
  Future<List<InterestItem>> getInterests() => throw UnimplementedError();
  @override
  Future<List<CountryItem>> getCountries() => throw UnimplementedError();
  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) =>
      throw UnimplementedError();
  @override
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) =>
      throw UnimplementedError();
}

const UserProfileResponse _saudiProfile = UserProfileResponse(
  interestIds: <String>['i1'],
  arabicName: 'راكان السالم',
  englishName: 'Rakan Alsalem',
  nationalityCode: 'SA',
  placeOfBirth: 'Riyadh',
  isSaudi: true,
  gender: AppGender.male,
  hasIdImage: true,
  hasAvatar: true,
  jobTitle: 'Engineer',
  jobTitleArabic: 'مهندس',
  organisationId: 'org-3',
  regionId: 'region-7',
  nationalId: '1000000008',
  dateOfBirth: '2000-01-31',
  saudiMobile: '0501234567',
);

const UserProfileResponse _internationalProfile = UserProfileResponse(
  interestIds: <String>['i2'],
  arabicName: 'راكان السالم',
  englishName: 'Rakan Alsalem',
  nationalityCode: 'EG',
  placeOfBirth: 'Cairo',
  isSaudi: false,
  gender: AppGender.male,
  hasIdImage: true,
  hasAvatar: true,
  organisationId: 'org-3',
  passportNumber: 'A1234567',
);

Future<void> _pump(WidgetTester tester, _FakeProfileRepository repo) async {
  final router = GoRouter(
    initialLocation: '/my-area',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.myArea,
        path: '/my-area',
        builder: (c, s) => const Scaffold(body: Text('MY-AREA')),
        routes: <RouteBase>[
          GoRoute(
            name: RouteNames.myMobile,
            path: 'mobile',
            builder: (c, s) => const MyMobileScreen(),
          ),
        ],
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_config),
        authControllerProvider.overrideWith(_SignedIn.new),
        profileRepositoryProvider.overrideWithValue(repo),
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
  unawaited(router.pushNamed(RouteNames.myMobile));
  await tester.pumpAndSettle();
}

void main() {
  group('MyMobileScreen (owner 2026-07-26 — add/edit phone, validate only)', () {
    testWidgets('pre-fills the stored Saudi mobile', (tester) async {
      final repo = _FakeProfileRepository(myProfile: _saudiProfile);
      await _pump(tester, repo);

      expect(find.text('Mobile number'), findsWidgets);
      expect(find.text('0501234567'), findsWidgets);
    });

    testWidgets('rejects a non-standard Saudi mobile without calling the API',
        (tester) async {
      final repo = _FakeProfileRepository(myProfile: _saudiProfile);
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), '12345');
      await tester.tap(find.byKey(const ValueKey<String>('myMobileSave')));
      await tester.pumpAndSettle();

      expect(repo.upserted, isNull);
      expect(
        find.textContaining('05XXXXXXXX'),
        findsOneWidget,
        reason: 'the C4 (D-371) Saudi shape message must show',
      );
    });

    testWidgets('an empty number is required, not silently saved',
        (tester) async {
      final repo = _FakeProfileRepository(myProfile: _saudiProfile);
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), '');
      await tester.tap(find.byKey(const ValueKey<String>('myMobileSave')));
      await tester.pumpAndSettle();

      expect(repo.upserted, isNull);
      expect(find.text('Mobile number is required'), findsOneWidget);
    });

    testWidgets('a valid save sends the new mobile and keeps every other field',
        (tester) async {
      final repo = _FakeProfileRepository(myProfile: _saudiProfile);
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), '0559876543');
      await tester.tap(find.byKey(const ValueKey<String>('myMobileSave')));
      await tester.pumpAndSettle();

      final sent = repo.upserted;
      expect(sent, isNotNull);
      expect(sent!.saudiMobile, '0559876543');
      // A mobile-only edit must null nothing else (the upsert is the only
      // write path and the service writes every field unconditionally).
      expect(sent.arabicName, 'راكان السالم');
      expect(sent.nationalId, '1000000008');
      expect(sent.organisationId, 'org-3');
      expect(sent.regionId, 'region-7');
      expect(sent.jobTitleArabic, 'مهندس');
      expect(sent.interestIds, <String>['i1']);
      // NO VERIFY — the screen pops straight back; there is no OTP step.
      expect(find.text('MY-AREA'), findsOneWidget);
    });

    testWidgets('a leading 00 international prefix is normalised to +',
        (tester) async {
      final repo = _FakeProfileRepository(myProfile: _internationalProfile);
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), '00201000000000');
      await tester.tap(find.byKey(const ValueKey<String>('myMobileSave')));
      await tester.pumpAndSettle();

      expect(repo.upserted?.internationalMobile, '+201000000000');
      expect(repo.upserted?.saudiMobile, isNull);
    });

    testWidgets('a server error stays on the screen and shows the message',
        (tester) async {
      final repo = _FakeProfileRepository(myProfile: _saudiProfile)
        ..throwOnSave = true;
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), '0559876543');
      await tester.tap(find.byKey(const ValueKey<String>('myMobileSave')));
      await tester.pumpAndSettle();

      expect(find.text('MY-AREA'), findsNothing);
      expect(find.text('save-boom'), findsOneWidget);
    });

    testWidgets('a load failure offers retry', (tester) async {
      final repo = _FakeProfileRepository(myProfile: _saudiProfile)
        ..throwOnLoad = true;
      await _pump(tester, repo);

      expect(find.text('load-boom'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });
  });
}
