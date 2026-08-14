import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/widgets/simf_checkbox_tile.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/sign_up_interests_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake profile repository for the interests screen (Page 007‑01) — returns
/// canned interests and records the single upsert + image upload, so the
/// pick → save → navigate glue is testable without HTTP. Only the three methods
/// this screen uses are implemented; the rest throw.
class _FakeProfileRepository implements ProfileRepository {
  _FakeProfileRepository({
    this.interests = _canned,
    this.throwOnSave = false,
  });

  static const List<InterestItem> _canned = <InterestItem>[
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

  List<InterestItem> interests;
  bool throwOnSave;
  bool throwOnUpload = false;
  bool throwOnAvatarUpload = false;
  bool throwOnLoadProfile = false;
  UpsertUserProfileRequest? upserted;
  bool uploadCalled = false;
  bool avatarUploadCalled = false;

  /// #14 edit-mode current profile from [getMyProfile]: a non-Saudi VIP
  /// (admin-assigned `profileTypeId`) carrying the fields a naive full-upsert
  /// edit would wipe (regionId, jobTitleArabic, iqama/passport/intl mobile) +
  /// a pre-selected interest (i2), so the round-trip + no-400 can be asserted.
  UserProfileResponse myProfile = const UserProfileResponse(
    interestIds: <String>['i2'],
    profileTypeId: 'vip-type-id',
    arabicName: 'راكان السالم',
    englishName: 'Rakan Alsalem',
    nationalityCode: 'EG',
    placeOfBirth: 'Cairo',
    isSaudi: false,
    gender: AppGender.male,
    hasIdImage: true,
    hasAvatar: true,
    jobTitle: 'Engineer',
    jobTitleArabic: 'مهندس',
    organisationId: 'org-3',
    regionId: 'region-7',
    iqamaNumber: '2000000009',
    passportNumber: 'A1234567',
    internationalMobile: '+201000000000',
  );

  @override
  Future<List<InterestItem>> getInterests() async => interests;

  @override
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) async {
    if (throwOnSave) {
      throw const ApiFailure(code: 'X', message: 'boom');
    }
    upserted = request;
    return const UserProfileResponse(
      interestIds: <String>['i1'],
      arabicName: '',
      englishName: '',
      nationalityCode: '',
      placeOfBirth: '',
      isSaudi: false,
      gender: AppGender.unspecified,
      hasIdImage: false,
      hasAvatar: false,
    );
  }

  @override
  Future<bool> uploadIdImage({
    required List<int> bytes,
    required String filename,
  }) async {
    uploadCalled = true;
    if (throwOnUpload) {
      throw const ApiFailure(code: 'X', message: 'upload-boom');
    }
    return true;
  }

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async {
    avatarUploadCalled = true;
    if (throwOnAvatarUpload) {
      throw const ApiFailure(code: 'X', message: 'avatar-boom');
    }
    return true;
  }

  @override
  Future<UserProfileResponse> getMyProfile() async {
    if (throwOnLoadProfile) {
      throw const ApiFailure(code: 'X', message: 'load-boom');
    }
    return myProfile;
  }
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

const SignUpProfileDraft _draft = SignUpProfileDraft(
  request: UpsertUserProfileRequest(
    interestIds: <String>[],
    arabicName: 'راكان السالم',
    englishName: 'Rakan Alsalem',
    nationalityCode: 'SA',
    placeOfBirth: 'Riyadh',
    isSaudi: true,
    gender: AppGender.male,
    nationalId: '1000000008',
    dateOfBirth: '2000-01-31',
  ),
);

Future<void> _pump(
  WidgetTester tester,
  _FakeProfileRepository repo, {
  SignUpProfileDraft? draft = _draft,
}) async {
  final router = GoRouter(
    initialLocation: '/sign-up/interests',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signUpInterests,
        path: '/sign-up/interests',
        builder: (c, s) => SignUpInterestsScreen(draft: draft),
      ),
      GoRoute(
        name: RouteNames.registrationSuccess,
        path: '/registration/success',
        builder: (c, s) => const Scaffold(body: Text('REG-SUCCESS')),
      ),
      GoRoute(
        name: RouteNames.signUpVisitor,
        path: '/sign-up/visitor',
        builder: (c, s) => const Scaffold(body: Text('DATA')),
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

bool _saveEnabled(WidgetTester tester) {
  final save = find.widgetWithText(FilledButton, 'Continue');
  return tester.widget<FilledButton>(save).onPressed != null;
}

/// #14 — pumps the SAME interests screen in EDIT mode, opened from a stub
/// My-Area screen so the edit save's pop-back returns somewhere real.
Future<void> _pumpEdit(
  WidgetTester tester,
  _FakeProfileRepository repo, {
  String locale = 'en',
}) async {
  final router = GoRouter(
    initialLocation: '/my-area',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.myArea,
        path: '/my-area',
        builder: (c, s) => Scaffold(
          body: Center(
            child: FilledButton(
              onPressed: () => c.pushNamed(RouteNames.myInterests),
              child: const Text('OPEN-INTERESTS'),
            ),
          ),
        ),
      ),
      GoRoute(
        name: RouteNames.myInterests,
        path: '/my-area/interests',
        builder: (c, s) => const SignUpInterestsScreen(editMode: true),
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
        locale: Locale(locale),
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
  await tester.tap(find.text('OPEN-INTERESTS'));
  await tester.pumpAndSettle();
}

void main() {
  group('SignUpInterestsScreen (Page 007‑01)', () {
    testWidgets('renders interests + counter; Save disabled until one picked',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      expect(find.text('My interests'), findsOneWidget);
      expect(find.text('Naval Defence'), findsOneWidget);
      expect(find.text('0 / 10 selected'), findsOneWidget);
      expect(_saveEnabled(tester), isFalse);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();

      expect(find.text('1 / 10 selected'), findsOneWidget);
      expect(_saveEnabled(tester), isTrue);
    });

    testWidgets('Save fires ONE upsert carrying the draft data + interestIds, '
        'then navigates to registration success', (tester) async {
      final repo = _FakeProfileRepository();
      await _pump(tester, repo);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(repo.upserted, isNotNull);
      expect(repo.upserted!.interestIds, contains('i1'));
      // The carried Page-007 data rides on the same single request.
      expect(repo.upserted!.arabicName, 'راكان السالم');
      expect(repo.upserted!.nationalId, '1000000008');
      expect(find.text('REG-SUCCESS'), findsOneWidget);
    });

    testWidgets('an empty interests lookup shows the empty state',
        (tester) async {
      await _pump(
        tester,
        _FakeProfileRepository(interests: const <InterestItem>[]),
      );

      expect(find.text('No interests available'), findsOneWidget);
      expect(_saveEnabled(tester), isFalse);
    });

    testWidgets('a save failure shows the error and keeps the selection',
        (tester) async {
      final repo = _FakeProfileRepository(throwOnSave: true);
      await _pump(tester, repo);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(find.text('boom'), findsOneWidget);
      expect(find.text('REG-SUCCESS'), findsNothing);
      expect(find.text('1 / 10 selected'), findsOneWidget);
    });

    // D-684 — image upload + the mandatory-upload-blocks behaviour moved to the
    // profile step (Page 007); it is covered by sign_up_visitor_screen_test
    // now. This screen only adds the interests to the already-saved profile.

    testWidgets('a direct open with no draft shows the recover state',
        (tester) async {
      await _pump(tester, _FakeProfileRepository(), draft: null);

      final recover = find.byType(FilledButton);
      expect(recover, findsOneWidget);
      await tester.tap(recover);
      await tester.pumpAndSettle();

      expect(find.text('DATA'), findsOneWidget);
    });
  });

  group('SignUpInterestsScreen edit mode (#14 — My interests)', () {
    testWidgets('self-loads the profile and pre-selects the saved interests, '
        'Save enabled', (tester) async {
      await _pumpEdit(tester, _FakeProfileRepository());

      // No recover state in edit mode; the saved interest (i2) is pre-selected.
      expect(find.text('DATA'), findsNothing);
      expect(find.text('My interests'), findsOneWidget);
      expect(find.text('1 / 10 selected'), findsOneWidget);
      final save = find.widgetWithText(FilledButton, 'Save');
      expect(save, findsOneWidget);
      expect(tester.widget<FilledButton>(save).onPressed, isNotNull);
    });

    testWidgets('save re-POSTs the FULL profile — interests change while '
        'region + Arabic job title are preserved (no wipe)', (tester) async {
      final repo = _FakeProfileRepository();
      await _pumpEdit(tester, repo);

      // Add a second interest, then save.
      await tester.tap(find.text('Naval Defence')); // i1
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(repo.upserted, isNotNull);
      // Interests updated to include both the pre-selected and the new pick.
      expect(repo.upserted!.interestIds, containsAll(<String>['i2', 'i1']));
      // Every field a naive full-upsert edit would wipe survives — incl. the
      // non-Saudi cohort (iqama/passport/international mobile).
      expect(repo.upserted!.regionId, 'region-7');
      expect(repo.upserted!.jobTitleArabic, 'مهندس');
      expect(repo.upserted!.organisationId, 'org-3');
      expect(repo.upserted!.arabicName, 'راكان السالم');
      expect(repo.upserted!.iqamaNumber, '2000000009');
      expect(repo.upserted!.passportNumber, 'A1234567');
      expect(repo.upserted!.internationalMobile, '+201000000000');
      // Blocker guard: the admin-assigned profileTypeId is NOT echoed, so the
      // server never 400s a VIP/VVIP/Staff/partner editing their interests.
      expect(repo.upserted!.profileTypeId, isNull);
      // Popped back to My-Area.
      expect(find.text('OPEN-INTERESTS'), findsOneWidget);
    });

    testWidgets('a profile-load failure shows the error, no upsert',
        (tester) async {
      final repo = _FakeProfileRepository()..throwOnLoadProfile = true;
      await _pumpEdit(tester, repo);

      expect(find.text('load-boom'), findsOneWidget);
      expect(repo.upserted, isNull);
    });

    testWidgets('a save failure shows the error, keeps the selection, no pop',
        (tester) async {
      final repo = _FakeProfileRepository(throwOnSave: true);
      await _pumpEdit(tester, repo);

      await tester.tap(find.text('Naval Defence')); // i1 (now i2 + i1 = 2)
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(find.text('boom'), findsOneWidget);
      // No pop — still on the interests screen; selection + counter preserved.
      expect(find.text('OPEN-INTERESTS'), findsNothing);
      expect(find.text('2 / 10 selected'), findsOneWidget);
      expect(find.text('Your interests were updated'), findsNothing);
    });

    testWidgets('a successful edit shows the "interests updated" toast',
        (tester) async {
      final repo = _FakeProfileRepository();
      await _pumpEdit(tester, repo);

      await tester.tap(find.text('Naval Defence'));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pump(); // resolve the async save -> showSnackBar + pop
      // Let the SnackBar animate in before it auto-dismisses.
      await tester.pump(const Duration(milliseconds: 300));

      expect(find.text('Your interests were updated'), findsOneWidget);
    });

    testWidgets('the 10-interest cap blocks an 11th and shows the max toast',
        (tester) async {
      final many = List<InterestItem>.generate(
        11,
        (i) => InterestItem(
          id: 'c$i',
          name: 'Topic $i',
          nameArabic: 'موضوع $i',
          displayOrder: i,
        ),
      );
      final repo = _FakeProfileRepository(interests: many)
        ..myProfile = const UserProfileResponse(
          interestIds: <String>[],
          arabicName: 'x',
          englishName: 'x',
          nationalityCode: 'SA',
          placeOfBirth: 'Riyadh',
          isSaudi: true,
          gender: AppGender.male,
          hasIdImage: true,
          hasAvatar: true,
        );
      await _pumpEdit(tester, repo);

      for (var i = 0; i < 10; i++) {
        await tester.ensureVisible(find.text('Topic $i'));
        await tester.tap(find.text('Topic $i'));
        await tester.pump();
      }
      expect(find.text('10 / 10 selected'), findsOneWidget);

      await tester.ensureVisible(find.text('Topic 10'));
      await tester.tap(find.text('Topic 10')); // attempt the 11th
      await tester.pump();

      expect(find.text('You can pick at most 10 interests'), findsOneWidget);
      expect(find.text('10 / 10 selected'), findsOneWidget); // unchanged
    });

    testWidgets('renders right-to-left in Arabic (edit mode)', (tester) async {
      await _pumpEdit(tester, _FakeProfileRepository(), locale: 'ar');

      expect(find.text('اهتماماتي'), findsOneWidget); // interestsTitle (ar)
      final dir = Directionality.of(tester.element(find.text('اهتماماتي')));
      expect(dir, TextDirection.rtl);
    });

    // The "show me in Meet People Like You" opt-in was removed from the app
    // (owner 2026-07-24) — that visibility toggle now lives only in the Control
    // Panel. The app must NOT render it, and a full profile re-POST (an
    // interests-only edit) must round-trip the CP-set value untouched.
    testWidgets('the Meet-People opt-in is gone; its CP value round-trips',
        (tester) async {
      final repo = _FakeProfileRepository()
        ..myProfile = const UserProfileResponse(
          interestIds: <String>['i2'],
          profileTypeId: 'other-type-id',
          arabicName: 'شركة الشحن',
          englishName: 'Shipping Co',
          nationalityCode: 'SA',
          placeOfBirth: 'Riyadh',
          isSaudi: true,
          gender: AppGender.male,
          hasIdImage: true,
          hasAvatar: true,
          isForVisitor: false,
          showInMeetLikeYou: false,
        );
      await _pumpEdit(tester, repo);

      expect(find.byType(SimfCheckboxTile), findsNothing);

      // Saving an interests-only change must preserve the CP-set visibility.
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(repo.upserted, isNotNull);
      expect(repo.upserted!.showInMeetLikeYou, isFalse);
    });
  });
}
