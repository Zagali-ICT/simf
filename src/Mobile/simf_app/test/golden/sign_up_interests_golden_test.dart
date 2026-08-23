@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/sign_up_profile_draft.dart';
import 'package:simf_app/features/account/sign_up_interests_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the sign-up interests screen against Figma frame
/// **505:1083** (اهتماماتي · Page 007-01). Regenerate:
///   flutter test --update-goldens test/golden/sign_up_interests_golden_test.dart
///
/// Frame parity: the navy scaffold + sweep, the back/title header, the
/// اختر اهتماماتك heading + helper, the two-column pill grid (gold selected /
/// navyDeep-bordered unselected), the n/10 counter, and the gold متابعة button
/// pinned at the bottom. Captured with 2 of 10 interests pre-selected (counter
/// "2 / 10") to lock the selected + unselected pill states. This locks the
/// layout, typography, colour, spacing and RTL of the clean-code-frozen page
/// (D-550).
class _FakeProfileRepository implements ProfileRepository {
  static const List<InterestItem> _interests = <InterestItem>[
    InterestItem(
      id: 'i1',
      name: 'Cyber Security',
      nameArabic: 'الأمن السيبراني',
      displayOrder: 1,
    ),
    InterestItem(
      id: 'i2',
      name: 'Information Tech',
      nameArabic: 'تقنية المعلومات',
      displayOrder: 2,
    ),
    InterestItem(
      id: 'i3',
      name: 'R&D',
      nameArabic: 'البحوث والتطوير',
      displayOrder: 3,
    ),
    InterestItem(
      id: 'i4',
      name: 'Energy',
      nameArabic: 'الطاقة',
      displayOrder: 4,
    ),
    InterestItem(
      id: 'i5',
      name: 'Media',
      nameArabic: 'الإعلام',
      displayOrder: 5,
    ),
    InterestItem(
      id: 'i6',
      name: 'Maritime Navigation',
      nameArabic: 'الملاحة البحرية',
      displayOrder: 6,
    ),
    InterestItem(
      id: 'i7',
      name: 'Defence Industries',
      nameArabic: 'الصناعات الدفاعية',
      displayOrder: 7,
    ),
    InterestItem(
      id: 'i8',
      name: 'Investment',
      nameArabic: 'الاستثمار وريادة الأعمال',
      displayOrder: 8,
    ),
    InterestItem(
      id: 'i9',
      name: 'Engineering',
      nameArabic: 'الهندسة',
      displayOrder: 9,
    ),
    InterestItem(
      id: 'i10',
      name: 'AI',
      nameArabic: 'الذكاء الاصطناعي',
      displayOrder: 10,
    ),
  ];

  @override
  Future<void> deleteMyAccount() async {}

  @override
  Future<List<InterestItem>> getInterests() async => _interests;

  @override
  Future<UserProfileResponse> getMyProfile() => throw UnimplementedError();
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
  @override
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) =>
      throw UnimplementedError();
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
}

const SignUpProfileDraft _draft = SignUpProfileDraft(
  request: UpsertUserProfileRequest(
    // Two pre-selected interests -> the counter reads "2 / 10".
    interestIds: <String>['i1', 'i6'],
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

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Sign-up interests @375x812 — Figma 505:1083 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sign-up/interests',
      routes: <RouteBase>[
        GoRoute(
          path: '/sign-up/interests',
          name: RouteNames.signUpInterests,
          builder: (_, __) => const SignUpInterestsScreen(draft: _draft),
        ),
        GoRoute(
          path: '/sign-up/visitor',
          name: RouteNames.signUpVisitor,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/registration-success',
          name: RouteNames.registrationSuccess,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          profileRepositoryProvider.overrideWithValue(_FakeProfileRepository()),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
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

    await expectLater(
      find.byType(SignUpInterestsScreen),
      matchesGoldenFile('goldens/interests_505-1083.png'),
    );
  });
}
