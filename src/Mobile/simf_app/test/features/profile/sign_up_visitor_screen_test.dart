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
  _FakeProfileRepository({
    UserProfileResponse? profile,
    this.throwOnLoad = false,
    List<CountryItem>? countries,
  })  : profile = profile ?? _emptyProfile,
        countries = countries ?? _defaultCountries;

  static const List<CountryItem> _defaultCountries = <CountryItem>[
    CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
    CountryItem(code: 'US', name: 'United States', nameArabic: 'أمريكا'),
  ];

  /// The country lookup the screen builds the nationality picker from. A test
  /// can omit Saudi Arabia to prove the SA fallback can leave the code null.
  final List<CountryItem> countries;

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
  // D-375 — lets a test fail ONLY the profile-types lookup (the per-switch
  // fetch), exercising the picker's inline loading/retry state.
  bool throwOnProfileTypes = false;
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
  Future<List<CountryItem>> getCountries() async => countries;

  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) async {
    lastProfileTypesIsVisitor = isVisitor;
    if (throwOnProfileTypes) {
      throw const ApiFailure(code: 'X', message: 'lookup boom');
    }
    // C5 (D-371) — the audience side carries the single "Normal" type the
    // screen auto-locks to; the partner ("Other") side carries a pickable
    // list, mirroring the seeded data shape.
    if (isVisitor == false) {
      return const <ProfileTypeItem>[
        ProfileTypeItem(
          id: 't2',
          name: 'Media',
          nameArabic: 'إعلامي',
          isVisitor: false,
        ),
      ];
    }
    return const <ProfileTypeItem>[
      ProfileTypeItem(
        id: 'n1',
        name: 'Normal',
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

/// The draft Next carried to the interests stub route (null until Next runs).
SignUpProfileDraft? capturedDraft;

Future<void> _pump(
  WidgetTester tester,
  _FakeProfileRepository repo, {
  Locale locale = const Locale('en'),
}) async {
  capturedDraft = null;
  final router = GoRouter(
    initialLocation: '/sign-up/visitor',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signUpVisitor,
        path: '/sign-up/visitor',
        builder: (c, s) => const SignUpVisitorScreen(),
      ),
      // The Next destination — a stub so the navigation is observable; the
      // carried draft is captured so tests can assert what Next collected.
      GoRoute(
        name: RouteNames.signUpInterests,
        path: '/sign-up/interests',
        builder: (c, s) {
          final extra = s.extra;
          capturedDraft = extra is SignUpProfileDraft ? extra : null;
          return const Scaffold(body: Text('INTERESTS'));
        },
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

UserProfileResponse _completeProfile({
  AppGender gender = AppGender.male,
  bool hasIdImage = true, // C7 — a male profile needs a stored photo
}) =>
    UserProfileResponse(
      interestIds: const <String>['i1'],
      arabicName: 'راكان السالم',
      englishName: 'Rakan Alsalem',
      nationalityCode: 'SA',
      placeOfBirth: 'Riyadh',
      isSaudi: true,
      gender: gender,
      hasIdImage: hasIdImage,
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

    testWidgets('a first-time empty form blocks Next with required errors '
        'and shows the D-373 defaults (SA + national-ID branch)',
        (tester) async {
      final repo = _FakeProfileRepository();
      await _pump(tester, repo);

      // D-373 defaults: nationality pre-set to Saudi Arabia, which drives
      // the national-ID branch (no Iqama/Passport picker, no nationality
      // error on submit).
      expect(find.text('Saudi Arabia'), findsOneWidget);
      expect(find.text('National ID'), findsOneWidget);
      expect(find.text('Iqama'), findsNothing);

      await _tapNext(tester);

      // Stayed on the data screen (no navigation) and never saved.
      expect(find.text('INTERESTS'), findsNothing);
      expect(repo.upserted, isNull);
      expect(find.text('This field is required'), findsWidgets);
      expect(find.text('Nationality is required'), findsNothing);
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

    testWidgets(
        'a male profile without a stored photo blocks Next with the '
        'camera-capture error (C7 — D-371)', (tester) async {
      final repo = _FakeProfileRepository(
        profile: _completeProfile(hasIdImage: false),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsNothing);
      expect(
        find.text('A photo is required — capture it with the camera'),
        findsOneWidget,
      );
    });

    testWidgets(
        'a female profile without a photo proceeds — the image stays '
        'optional for women (C7 — D-371)', (tester) async {
      final repo = _FakeProfileRepository(
        profile: _completeProfile(
          gender: AppGender.female,
          hasIdImage: false,
        ),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsOneWidget);
    });

    testWidgets(
        'Visitor tab hides the profile-type picker and auto-locks Normal '
        '(C5 — D-371)', (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      // No picker under the Visitor tab — the type is locked, not chosen.
      expect(
        find.byKey(const ValueKey<String>('profileTypePicker')),
        findsNothing,
      );

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsOneWidget);
      expect(capturedDraft, isNotNull);
      // The draft carries the auto-locked Normal type id.
      expect(capturedDraft!.request.profileTypeId, equals('n1'));
    });

    testWidgets(
        'Other tab shows the picker and requires a pick (C5 — D-371)',
        (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      await tester.tap(find.text('Other'));
      await tester.pumpAndSettle();

      // The picker is back for the partner side.
      const picker = ValueKey<String>('profileTypePicker');
      expect(find.byKey(picker), findsOneWidget);

      // Without a pick, Next is blocked with the required error.
      await _tapNext(tester);
      expect(find.text('INTERESTS'), findsNothing);
      expect(
        find.text('A profile type selection is required'),
        findsOneWidget,
      );

      // Picking one unblocks Next and the draft carries it.
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Media').last);
      await tester.pumpAndSettle();
      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsOneWidget);
      expect(capturedDraft!.request.profileTypeId, equals('t2'));
    });

    testWidgets(
        'a failed Other profile-types lookup shows the inline retry — never '
        'a silently hidden picker (D-375)', (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      // Fail the per-switch lookup, then switch to Other.
      repo.throwOnProfileTypes = true;
      await tester.tap(find.text('Other'));
      await tester.pumpAndSettle();

      // The field area stays visible: inline error + retry, no dropdown.
      expect(find.text('Could not load the list.'), findsOneWidget);
      const retry = ValueKey<String>('profileTypeRetry');
      expect(find.byKey(retry), findsOneWidget);
      expect(
        find.byKey(const ValueKey<String>('profileTypePicker')),
        findsNothing,
      );

      // Recover + retry → the picker appears with the partner list.
      repo.throwOnProfileTypes = false;
      await tester.tap(find.byKey(retry));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const ValueKey<String>('profileTypePicker')),
        findsOneWidget,
      );
      expect(find.text('Could not load the list.'), findsNothing);
    });

    testWidgets(
        'picking a non-Saudi country via the searchable sheet switches the '
        'document section to Iqama / Passport (D-373)', (tester) async {
      await _pump(tester, _FakeProfileRepository());

      // Defaults: SA → national ID.
      expect(find.text('National ID'), findsOneWidget);

      // Open the searchable picker, filter, and pick the United States.
      const picker = ValueKey<String>('nationalityPicker');
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const ValueKey<String>('countrySearchField')),
        'United',
      );
      await tester.pumpAndSettle();
      // (The form behind the sheet still shows the current selection, so
      // only the filtered match is asserted.)
      await tester.tap(find.text('United States'));
      await tester.pumpAndSettle();

      // The derivation flips the document section (no switch — D-373).
      expect(find.text('Iqama'), findsOneWidget);
      expect(find.text('Passport'), findsOneWidget);
      expect(find.text('National ID'), findsNothing);
      expect(find.byType(SwitchListTile), findsNothing);

      // Picking SA back restores the national-ID branch.
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Saudi Arabia').last);
      await tester.pumpAndSettle();
      expect(find.text('National ID'), findsOneWidget);
      expect(find.text('Iqama'), findsNothing);
    });

    testWidgets(
        'a null nationality (no SA fallback) blocks Next with the inline '
        'picker error — it is not a FormField (D-373 gate)', (tester) async {
      // Country list without Saudi Arabia + a profile code that matches nothing
      // → the SA fallback cannot fire, so _nationalityCode stays null. Every
      // other field is valid (non-Saudi branch with a passport, female so no
      // photo), making the nationality picker the ONLY blocker. Before the
      // gate, Next navigated and sent nationalityCode: '' (a server 400).
      const profileNoNationality = UserProfileResponse(
        interestIds: <String>['i1'],
        arabicName: 'راكان السالم',
        englishName: 'Rakan Alsalem',
        nationalityCode: 'ZZ',
        placeOfBirth: 'Riyadh',
        isSaudi: false,
        gender: AppGender.female,
        hasIdImage: false,
        passportNumber: 'P1234567',
        dateOfBirth: '2000-01-31',
        organisationId: 'o1',
      );
      final repo = _FakeProfileRepository(
        profile: profileNoNationality,
        countries: const <CountryItem>[
          CountryItem(code: 'US', name: 'United States', nameArabic: 'أمريكا'),
        ],
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsNothing);
      expect(repo.upserted, isNull);
      expect(find.text('Nationality is required'), findsOneWidget);
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
