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
/// and records the upsert / image upload, so the screen's load → validate →
/// save → navigate glue is testable without a real HTTP client.
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
  UpsertUserProfileRequest? upserted;
  bool uploadCalled = false;

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
  Future<List<ProfileTypeItem>> getProfileTypes() async =>
      const <ProfileTypeItem>[
        ProfileTypeItem(
          id: 't1',
          name: 'Regular',
          nameArabic: 'عادي',
          isVisitor: true,
        ),
      ];

  @override
  Future<List<InterestItem>> getInterests() async => const <InterestItem>[
        InterestItem(
          id: 'i1',
          name: 'Naval Defence',
          nameArabic: 'الدفاع البحري',
          displayOrder: 1,
        ),
        InterestItem(
          id: 'i2',
          name: 'AI',
          nameArabic: 'الذكاء الاصطناعي',
          displayOrder: 2,
        ),
      ];

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
  }) async {
    uploadCalled = true;
    return true;
  }
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
      GoRoute(
        name: RouteNames.registrationSuccess,
        path: '/registration/success',
        builder: (c, s) => const Scaffold(body: Text('REG-SUCCESS')),
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
    );

Future<void> _tapSave(WidgetTester tester) async {
  final save = find.widgetWithText(FilledButton, 'Save');
  await tester.ensureVisible(save);
  await tester.pumpAndSettle();
  await tester.tap(save);
  await tester.pumpAndSettle();
}

void main() {
  group('SignUpVisitorScreen (Page 007)', () {
    testWidgets('loads the lookups and renders the form sections',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      expect(find.text('Personal'), findsOneWidget);
      expect(find.text('Affiliation'), findsOneWidget);
      expect(find.text('Interests'), findsOneWidget);
      expect(find.text('Nationality'), findsWidgets);
      expect(find.text('0 / 10 selected'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Save'), findsOneWidget);
    });

    testWidgets('a first-time empty profile blocks save with required errors',
        (tester) async {
      final repo = _FakeProfileRepository();
      await _pump(tester, repo);

      await _tapSave(tester);

      expect(repo.upserted, isNull);
      expect(find.text('This field is required'), findsWidgets);
      expect(find.text('Nationality is required'), findsOneWidget);
      expect(find.text('Date of birth is required'), findsOneWidget);
      expect(find.text('Pick at least one interest'), findsOneWidget);
    });

    testWidgets('a pre-filled valid profile upserts and routes to '
        'registration-status', (tester) async {
      final repo = _FakeProfileRepository(profile: _completeProfile());
      await _pump(tester, repo);

      await _tapSave(tester);

      expect(repo.upserted, isNotNull);
      expect(repo.upserted!.interestIds, contains('i1'));
      expect(repo.upserted!.nationalId, '1000000008');
      expect(repo.upserted!.isSaudi, isTrue);
      expect(find.text('REG-SUCCESS'), findsOneWidget);
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

    testWidgets('selecting an interest updates the counter', (tester) async {
      await _pump(tester, _FakeProfileRepository());

      final chip = find.widgetWithText(FilterChip, 'Naval Defence');
      await tester.ensureVisible(chip);
      await tester.tap(chip);
      await tester.pumpAndSettle();

      expect(find.text('1 / 10 selected'), findsOneWidget);
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

      expect(find.widgetWithText(FilledButton, 'Save'), findsOneWidget);
      expect(repo.loadCalls, greaterThanOrEqualTo(2));
    });
  });
}
