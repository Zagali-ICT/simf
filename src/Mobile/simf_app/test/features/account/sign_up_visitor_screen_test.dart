import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/region_repository.dart';
import 'package:simf_app/features/account/sign_up_visitor_screen.dart';
import 'package:simf_app/features/account/widgets/mobile_field.dart';
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
    hasAvatar: false,
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

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

/// The draft Next carried to the interests stub route (null until Next runs).
SignUpProfileDraft? capturedDraft;

/// The 13 official Saudi regions the API serves (D-547), matching the const
/// `saudiRegions` codes so a prefilled region resolves under either source.
const List<RegionItem> _apiRegions = <RegionItem>[
  RegionItem(code: 'riyadh', name: 'Riyadh', nameArabic: 'منطقة الرياض'),
  RegionItem(code: 'makkah', name: 'Makkah', nameArabic: 'منطقة مكة المكرمة'),
  RegionItem(
    code: 'madinah',
    name: 'Al Madinah',
    nameArabic: 'منطقة المدينة المنورة',
  ),
  RegionItem(
    code: 'eastern',
    name: 'Eastern Province',
    nameArabic: 'المنطقة الشرقية',
  ),
  RegionItem(code: 'asir', name: 'Asir', nameArabic: 'منطقة عسير'),
  RegionItem(code: 'tabuk', name: 'Tabuk', nameArabic: 'منطقة تبوك'),
  RegionItem(code: 'hail', name: 'Hail', nameArabic: 'منطقة حائل'),
  RegionItem(
    code: 'northern',
    name: 'Northern Borders',
    nameArabic: 'منطقة الحدود الشمالية',
  ),
  RegionItem(code: 'jazan', name: 'Jazan', nameArabic: 'منطقة جازان'),
  RegionItem(code: 'najran', name: 'Najran', nameArabic: 'منطقة نجران'),
  RegionItem(code: 'bahah', name: 'Al Bahah', nameArabic: 'منطقة الباحة'),
  RegionItem(code: 'jawf', name: 'Al Jawf', nameArabic: 'منطقة الجوف'),
  RegionItem(code: 'qassim', name: 'Al Qassim', nameArabic: 'منطقة القصيم'),
];

Future<void> _pump(
  WidgetTester tester,
  _FakeProfileRepository repo, {
  Locale locale = const Locale('en'),
  Override? regionsOverride,
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
        // D-547 — the place-of-birth picker reads regionsProvider; default to
        // the 13 API regions so it resolves deterministically. A test can pass
        // its own override to exercise the const-list fallback on error.
        regionsOverride ??
            regionsProvider.overrideWith((ref) async => _apiRegions),
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
  bool hasIdImage = true, // ID document — mandatory for everyone
  bool hasAvatar = true, // face photo — mandatory for men, optional for women
  String? plateNumber,
  String placeOfBirth = 'Riyadh',
}) =>
    UserProfileResponse(
      interestIds: const <String>['i1'],
      // 2–4 parts in one script (the name rules require a full name).
      arabicName: 'راكان عبدالله أحمد السالم',
      englishName: 'Rakan Abdullah Ahmed Alsalem',
      nationalityCode: 'SA',
      placeOfBirth: placeOfBirth,
      isSaudi: true,
      gender: gender,
      hasIdImage: hasIdImage,
      hasAvatar: hasAvatar,
      nationalId: '1000000008', // matches ^1\d{9}$ and is Luhn-valid
      dateOfBirth: '2000-01-31',
      organisationId: 'o1', // B3 — D-221: organisation is now required
      plateNumber: plateNumber,
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

    testWidgets('prefills the plate dropdowns from a stored code and carries it '
        'to the draft (D-459)', (tester) async {
      final repo = _FakeProfileRepository(
        profile: _completeProfile(plateNumber: 'ABJ1234'),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      // _setPlateFromCode populated the 3 dropdowns + digits; _syncPlate
      // reassembled them, so the draft carries the same plate.
      expect(find.text('INTERESTS'), findsOneWidget);
      expect(capturedDraft, isNotNull);
      expect(capturedDraft!.request.plateNumber, 'ABJ1234');
    });

    testWidgets('a digits-first stored plate keeps its order on prefill — it is '
        'not silently reordered to letters-first (D-471)', (tester) async {
      // A plate is valid in either order; the canonical code preserves it. A
      // stored "1234ABJ" (digits-first) must round-trip unchanged, not become
      // "ABJ1234" (the pre-fix reorder bug).
      final repo = _FakeProfileRepository(
        profile: _completeProfile(plateNumber: '1234ABJ'),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsOneWidget);
      expect(capturedDraft, isNotNull);
      expect(capturedDraft!.request.plateNumber, '1234ABJ');
    });

    testWidgets('a Saudi profile shows the birth-location region dropdown with '
        'the stored region selected (D-469)', (tester) async {
      // _completeProfile is Saudi with placeOfBirth "Riyadh" (a region name), so
      // the region dropdown is prefilled (matched from the stored name).
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      expect(find.text('Riyadh'), findsWidgets);
    });

    testWidgets('a region stored in Arabic still selects under an English UI — '
        'the dropdown is code-keyed (D-469)', (tester) async {
      // A visitor registered under an Arabic session stored "منطقة الرياض"; the
      // English-locale render must still resolve it to the Riyadh option.
      final repo = _FakeProfileRepository(
        profile: _completeProfile(placeOfBirth: 'منطقة الرياض'),
      );
      await _pump(tester, repo);

      // regionByName matched the Arabic name to the riyadh code, so the
      // English-locale dropdown shows "Riyadh" (not the empty placeholder).
      expect(find.text('Riyadh'), findsWidgets);
      expect(find.text('Select region'), findsNothing);
    });

    testWidgets('the birth-region field opens the shared searchable sheet and '
        'picks a region (D-470)', (tester) async {
      // Saudi profile with no stored region → the picker shows the placeholder.
      final repo = _FakeProfileRepository(profile: _completeProfile(placeOfBirth: ''));
      await _pump(tester, repo);

      const picker = ValueKey<String>('birthRegionPicker');
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();

      // Same searchable sheet as the country picker (type-to-filter).
      final search = find.byKey(const ValueKey<String>('birthRegionSearchField'));
      expect(search, findsOneWidget);
      await tester.enterText(search, 'Eastern');
      await tester.pumpAndSettle();
      await tester.tap(find.text('Eastern Province'));
      await tester.pumpAndSettle();

      // The field now shows the picked region.
      expect(find.text('Eastern Province'), findsOneWidget);
    });

    testWidgets(
        'the birth-region picker lists the API regions when regionsProvider has '
        'data (D-547)', (tester) async {
      // The sheet lists the API regions — use a region whose API English name
      // differs from the const list so the source is observable.
      final repo =
          _FakeProfileRepository(profile: _completeProfile(placeOfBirth: ''));
      await _pump(
        tester,
        repo,
        regionsOverride: regionsProvider.overrideWith(
          (ref) async => const <RegionItem>[
            RegionItem(
              code: 'eastern',
              name: 'Eastern API Region',
              nameArabic: 'المنطقة الشرقية',
            ),
          ],
        ),
      );

      const picker = ValueKey<String>('birthRegionPicker');
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();

      // The API name (not the const "Eastern Province") is what the sheet shows.
      expect(find.text('Eastern API Region'), findsOneWidget);
      expect(find.text('Eastern Province'), findsNothing);
    });

    testWidgets(
        'the birth-region picker falls back to the const list when '
        'regionsProvider errors — it does not throw on build (D-547)',
        (tester) async {
      final repo =
          _FakeProfileRepository(profile: _completeProfile(placeOfBirth: ''));
      await _pump(
        tester,
        repo,
        regionsOverride: regionsProvider.overrideWith(
          (ref) async => throw const ApiFailure(code: 'X', message: 'boom'),
        ),
      );

      const picker = ValueKey<String>('birthRegionPicker');
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();

      // The const saudiRegions fallback is shown (its canonical English names).
      expect(find.text('Eastern Province'), findsOneWidget);
    });

    testWidgets('a plate-letter field opens the shared searchable sheet (D-470)',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      const picker = ValueKey<String>('plateLetter1');
      await tester.ensureVisible(find.byKey(picker));
      await tester.tap(find.byKey(picker));
      await tester.pumpAndSettle();

      // The shared search sheet is up over the 17 plate letters.
      expect(
        find.byKey(const ValueKey<String>('plateLetterSearch1')),
        findsOneWidget,
      );
    });

    testWidgets('a profile missing only the organisation blocks Next (B3 — D-221)',
        (tester) async {
      // Every field valid except the organisation → the required-organisation
      // gate keeps the desk on this screen with an inline error.
      const profileNoOrg = UserProfileResponse(
        interestIds: <String>['i1'],
        arabicName: 'راكان عبدالله أحمد السالم',
        englishName: 'Rakan Abdullah Ahmed Alsalem',
        nationalityCode: 'SA',
        placeOfBirth: 'Riyadh',
        isSaudi: true,
        gender: AppGender.male,
        hasIdImage: true,
        hasAvatar: true,
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
        'a profile without an ID document blocks Next with the ID-required '
        'hint (two-photo split — mandatory for all)', (tester) async {
      final repo = _FakeProfileRepository(
        profile: _completeProfile(hasIdImage: false),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsNothing);
      expect(find.text('An ID image is required'), findsOneWidget);
    });

    testWidgets(
        'a male profile without a face photo blocks Next, the face-required '
        'hint hidden until Next (two-photo split — face mandatory for men)',
        (tester) async {
      final repo = _FakeProfileRepository(
        // ID present, face missing → only the face gate blocks a male.
        profile: _completeProfile(hasAvatar: false),
      );
      await _pump(tester, repo);

      // The required hint stays hidden until a blocked Next (D-674) — like the
      // text-field validators, not surfaced up front in grey.
      expect(
        find.text('A face photo is required — capture it with the camera'),
        findsNothing,
      );

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsNothing);
      expect(
        find.text('A face photo is required — capture it with the camera'),
        findsOneWidget,
      );
    });

    testWidgets(
        'a female profile without a face photo proceeds — the face photo stays '
        'optional for women (ID document still present)', (tester) async {
      final repo = _FakeProfileRepository(
        profile: _completeProfile(
          gender: AppGender.female,
          hasAvatar: false,
        ),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsOneWidget);
    });

    testWidgets(
        'the Arabic-name field rejects non-Arabic characters at the keystroke '
        'and a too-short name blocks Next', (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      // The Arabic field allows only Arabic letters + spaces: typing Latin
      // text is filtered out, leaving the field unchanged.
      final arabicField = find.byType(TextFormField).first;
      await tester.enterText(arabicField, 'Ahmed 123');
      await tester.pump();
      expect(find.text('Ahmed 123'), findsNothing);

      // A three-part Arabic name fails the full-name (≥4 parts) rule on Next.
      await tester.enterText(arabicField, 'محمد عبدالله أحمد');
      await tester.pump();
      await _tapNext(tester);
      expect(find.text('INTERESTS'), findsNothing);
      expect(
        find.text('Enter your full name (at least 4 parts)'),
        findsWidgets,
      );
    });

    testWidgets(
        'a blocked Next surfaces the complete-the-fields message (D-434)',
        (tester) async {
      final repo = _FakeProfileRepository(
        profile: _completeProfile(hasIdImage: false),
      );
      await _pump(tester, repo);

      await _tapNext(tester);

      // Not a silent no-op: the bilingual prompt is shown as the blocked-Next
      // toast (the up-front banner was removed in D-674).
      expect(
        find.text(
          'Please complete the required fields below to finish your profile.',
        ),
        findsWidgets,
      );
      expect(find.text('INTERESTS'), findsNothing);
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
        arabicName: 'راكان عبدالله أحمد السالم',
        englishName: 'Rakan Abdullah Ahmed Alsalem',
        nationalityCode: 'ZZ',
        placeOfBirth: 'Riyadh',
        isSaudi: false,
        gender: AppGender.female,
        hasIdImage: true, // ID present so nationality is the sole blocker
        hasAvatar: false, // female — face photo not required
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

    testWidgets(
        'an unknown nationality code with SA present falls back to Saudi '
        'Arabia, so the gate is satisfied and Next proceeds (D-373 fallback)',
        (tester) async {
      // The positive arm of the same fallback the null-nationality test
      // exercises negatively: code 'ZZ' matches no country, but the default
      // list contains SA, so _nationalityCode resolves to 'SA' (national-ID
      // branch) — the gate passes and Next navigates.
      const profileUnknownCode = UserProfileResponse(
        interestIds: <String>['i1'],
        arabicName: 'راكان عبدالله أحمد السالم',
        englishName: 'Rakan Abdullah Ahmed Alsalem',
        nationalityCode: 'ZZ',
        placeOfBirth: 'Riyadh',
        isSaudi: true,
        gender: AppGender.female,
        hasIdImage: true, // ID present (required for all); face optional for women
        hasAvatar: false,
        nationalId: '1000000008',
        dateOfBirth: '2000-01-31',
        organisationId: 'o1',
      );
      // Default countries include SA, so the fallback can fire.
      final repo = _FakeProfileRepository(profile: profileUnknownCode);
      await _pump(tester, repo);

      expect(find.text('Saudi Arabia'), findsOneWidget);
      expect(find.text('National ID'), findsOneWidget);
      expect(find.text('Nationality is required'), findsNothing);

      await _tapNext(tester);

      expect(find.text('INTERESTS'), findsOneWidget);
    });

    testWidgets('the mobile field caps its length at 17 (maxLength set)',
        (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      // The shared MobileField wraps a TextFormField whose inner TextField has
      // maxLength 17 (covers Saudi 05XXXXXXXX / +9665XXXXXXXX / 009665XXXXXXXX
      // and international +/00 + country code + number).
      final mobileFinder = find.descendant(
        of: find.byType(MobileField),
        matching: find.byType(TextField),
      );
      expect(mobileFinder, findsOneWidget);
      final mobile = tester.widget<TextField>(mobileFinder);
      expect(mobile.maxLength, 17);

      // Over-long input is truncated to the cap.
      final mobileFormField = find.descendant(
        of: find.byType(MobileField),
        matching: find.byType(TextFormField),
      );
      await tester.enterText(mobileFormField, '0' * 30);
      await tester.pump();
      expect(mobile.controller!.text.length, 17);
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
