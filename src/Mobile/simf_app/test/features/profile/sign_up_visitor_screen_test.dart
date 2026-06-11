import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/profile/data/profile_models.dart';
import 'package:simf_app/features/profile/data/profile_repository.dart';
import 'package:simf_app/features/profile/sign_up_visitor_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake profile repository — returns canned lookups + a configurable profile,
/// and records the profile-types filter, so the data screen's load → validate →
/// Next glue is testable without a real HTTP client. (Reworked D-332: this screen
/// no longer saves — the upsert moved to the interests screen.)
class _FakeProfileRepository implements ProfileRepository {
  _FakeProfileRepository({UserProfileResponse? profile, this.throwOnLoad = false})
      : profile = profile ?? _emptyProfile;

  static const UserProfileResponse _emptyProfile = UserProfileResponse(
    interestIds: <String>[],
    arabicName: '',
    englishName: '',
    nationalityCode: '',
    placeOfBirth: '',
    isSaudi: false,
    gender: AppGender.unspecified,
    hasIdImage: false,
  );

  UserProfileResponse profile;
  bool throwOnLoad;
  int loadCalls = 0;
  bool? lastProfileTypesIsVisitor;
  UpsertUserProfileRequest? upserted;

  @override
  Future<UserProfileResponse> getMyProfile() async {
    loadCalls++;
    if (throwOnLoad) {
      throw const ApiFailure(code: 'X', message: 'boom');
    }
    return profile;
  }

  @override
  Future<List<CountryItem>> getCountries() async => const <CountryItem>[
        CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
        CountryItem(code: 'US', name: 'United States', nameArabic: 'أمريكا'),
      ];

  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) async {
    lastProfileTypesIsVisitor = isVisitor;
    return const <ProfileTypeItem>[
      ProfileTypeItem(
        id: 't1',
        name: 'Regular',
        nameArabic: 'عادي',
        isVisitor: true,
      ),
    ];
  }

  @override
  Future<List<InterestItem>> getInterests() async => const <InterestItem>[];

  @override
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) async =>
      const <OrganisationItem>[];

  @override
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) async {
    upserted = request;
    return profile;
  }

  @override
  Future<bool> uploadIdImage({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

Future<void> _pump(
  WidgetTester tester,
  _FakeProfileRepository repo, {
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/sign-up/visitor',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signUpVisitor,
        path: '/sign-up/visitor',
        builder: (c, s) => const SignUpVisitorScreen(),
      ),
      // The Next destination — a stub so the navigation is observable.
      GoRoute(
        name: RouteNames.signUpInterests,
        path: '/sign-up/interests',
        builder: (c, s) => const Scaffold(body: Text('INTERESTS')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        profileRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
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

UserProfileResponse _completeProfile() => const UserProfileResponse(
      interestIds: <String>['i1'],
      arabicName: 'راكان السالم',
      englishName: 'Rakan Alsalem',
      nationalityCode: 'SA',
      placeOfBirth: 'Riyadh',
      isSaudi: true,
      gender: AppGender.male,
      hasIdImage: false,
      nationalId: '1000000008', // matches ^1\d{9}$ and is Luhn-valid
      dateOfBirth: '2000-01-31',
      organisationId: 'o1', // B3 — D-221: organisation is now required
    );

Future<void> _tapNext(WidgetTester tester) async {
  final next = find.widgetWithText(FilledButton, 'Next');
  await tester.ensureVisible(next);
  await tester.pumpAndSettle();
  await tester.ensureVisible(next);
  await tester.tap(next);
  await tester.pumpAndSettle();
}

void main() {
  group('SignUpVisitorScreen (Page 007 — profile data)', () {
    testWidgets('renders the type filter + data sections and a Next button',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      expect(find.text('Visitor'), findsOneWidget);
      expect(find.text('Other'), findsOneWidget);
      expect(find.text('Nationality'), findsWidgets);
      expect(find.widgetWithText(FilledButton, 'Next'), findsOneWidget);
      // Interests are NOT on this screen anymore (D-332).
      expect(find.text('Interests'), findsNothing);
      expect(find.text('0 / 10 selected'), findsNothing);
    });

    testWidgets('نوع التسجيل filters the ProfileType picker via ?isVisitor=',
        (tester) async {
      final repo = _FakeProfileRepository();
      await _pump(tester, repo);

      // Default Visitor → loaded with isVisitor=true.
      expect(repo.lastProfileTypesIsVisitor, isTrue);

      // Picking "Other" re-filters with isVisitor=false.
      await tester.tap(find.text('Other'));
      await tester.pumpAndSettle();
      expect(repo.lastProfileTypesIsVisitor, isFalse);
    });

    testWidgets('a first-time empty form blocks Next with required errors',
        (tester) async {
      final repo = _FakeProfileRepository();
      await _pump(tester, repo);

      await _tapNext(tester);

      // Stayed on the data screen (no navigation) and never saved.
      expect(find.text('INTERESTS'), findsNothing);
      expect(repo.upserted, isNull);
      expect(find.text('This field is required'), findsWidgets);
      expect(find.text('Nationality is required'), findsOneWidget);
      expect(find.text('Date of birth is required'), findsOneWidget);
    });

    testWidgets('valid data → Next navigates to interests (no save here)',
        (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      await _tapNext(tester);

      // Navigated to the interests screen; this screen did NOT upsert.
      expect(find.text('INTERESTS'), findsOneWidget);
      expect(repo.upserted, isNull);
    });

    testWidgets('a profile missing only the organisation blocks Next (B3 — D-221)',
        (tester) async {
      // Every field valid except the organisation → the required-organisation
      // gate keeps the desk on this screen with an inline error.
      const profileNoOrg = UserProfileResponse(
        interestIds: <String>['i1'],
        arabicName: 'راكان السالم',
        englishName: 'Rakan Alsalem',
        nationalityCode: 'SA',
        placeOfBirth: 'Riyadh',
        isSaudi: true,
        gender: AppGender.male,
        hasIdImage: false,
        nationalId: '1000000008',
        dateOfBirth: '2000-01-31',
        // organisationId intentionally null.
      );
      final repo = _FakeProfileRepository(profile: profileNoOrg);
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsNothing);
      expect(find.text('Pick your organisation from the list'), findsOneWidget);
    });

    testWidgets('a non-Saudi profile shows the Iqama / Passport document picker',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      expect(find.text('Iqama'), findsOneWidget); // segment label
      expect(find.text('Passport'), findsOneWidget);
      expect(find.text('National ID'), findsNothing);
    });

    testWidgets('toggling "Saudi national" switches to the National ID field',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      final toggle = find.byType(SwitchListTile);
      await tester.ensureVisible(toggle);
      await tester.tap(toggle);
      await tester.pumpAndSettle();

      expect(find.text('National ID'), findsOneWidget);
      expect(find.text('Passport'), findsNothing);
    });

    testWidgets('a load failure shows the retry, which reloads the form',
        (tester) async {
      final repo = _FakeProfileRepository(throwOnLoad: true);
      await _pump(tester, repo);

      expect(find.text('Could not load the form.'), findsOneWidget);
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);

      repo.throwOnLoad = false;
      await tester.tap(retry);
      await tester.pumpAndSettle();

      expect(find.widgetWithText(FilledButton, 'Next'), findsOneWidget);
      expect(repo.loadCalls, greaterThanOrEqualTo(2));
    });
  });
}
