@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/data/region_models.dart';
import 'package:simf_app/features/account/data/region_repository.dart';
import 'package:simf_app/features/account/sign_up_visitor_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the sign-up profile-data form against Figma frame
/// **168:2972** (إنشاء ملف شخصي · Page 007). Regenerate: flutter test
/// --update-goldens test/golden/sign_up_visitor_golden_test.dart
///
/// Frame parity: the shared navy `SimfFormScaffold` (back chevron + globe
/// toggle, logo + forum name) over the beige form card (the MaxWidthBody-capped
/// Material). Card head = title (24/w600) + avatar badge; the complete-profile
/// notice; the beige visitor/other tabs; then the login-style bordered fields,
/// gender pills, document/nationality lookups, attach boxes, the underlined
/// terms link and the gold التالي. Captured in the empty/default (visitor)
/// state — the field values are runtime data; what this locks is the layout,
/// typography, colour, spacing and RTL of the frozen page (D-368 parity; D-545
/// clean-code freeze). The frame is rendered tall because the API-required
/// date-of-birth / place-of-birth / national-ID fields are kept (D-368
/// deviation) so the form is longer than the 1656px Figma art.
class _FakeProfileRepository implements ProfileRepository {
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

  @override
  Future<UserProfileResponse> getMyProfile() async => _emptyProfile;

  @override
  Future<List<CountryItem>> getCountries() async => const <CountryItem>[
        CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
        CountryItem(code: 'US', name: 'United States', nameArabic: 'أمريكا'),
      ];

  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) async =>
      const <ProfileTypeItem>[
        ProfileTypeItem(
          id: 'n1',
          name: 'Normal',
          nameArabic: 'عادي',
          isVisitor: true,
        ),
      ];

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
  ) async =>
      _emptyProfile;

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

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Sign-up profile @375x2100 — Figma 168:2972 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 2100);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sign-up/visitor',
      routes: <RouteBase>[
        GoRoute(
          path: '/sign-up/visitor',
          name: RouteNames.signUpVisitor,
          builder: (_, __) => const SignUpVisitorScreen(),
        ),
        GoRoute(
          path: '/sign-up/interests',
          name: RouteNames.signUpInterests,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/',
          name: RouteNames.home,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          profileRepositoryProvider.overrideWithValue(_FakeProfileRepository()),
          // D-547 — the place-of-birth picker reads regionsProvider; resolve it
          // deterministically. The default (visitor, empty) render shows the
          // picker's placeholder hint, so the source does not alter the golden.
          regionsProvider.overrideWith((ref) async => const <RegionItem>[]),
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
      find.byType(SignUpVisitorScreen),
      matchesGoldenFile('goldens/sign_up_visitor_168-2972.png'),
    );
  });
}
