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
import 'package:simf_app/features/registration/registration_status_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Registration-status screen against Figma frame
/// **1701:3789** (حالة التسجيل, approved). Regenerate:
///   flutter test --update-goldens test/golden/registration_status_golden_test.dart
///
/// Frame parity expected: a navy gate (no bottom nav), a back + centred title
/// header, a 104px green ring around the check, a white "تم اعتماد حسابك"
/// headline over the beige message, a full-width gold "متابعة" button, and a
/// muted "تسجيل الخروج" link beneath it. RTL throughout.

CurrentUser _approvedUser() => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'زائر',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _approvedUser(),
    );

class _ApprovedAuthController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());

  @override
  Future<CurrentUser> refreshCurrentUser() async => _approvedUser();
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Registration-status @375x812 — Figma 1701:3789 (approved, Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/registration/status',
      routes: <RouteBase>[
        GoRoute(
          name: RouteNames.registrationStatus,
          path: '/registration/status',
          builder: (_, __) => const RegistrationStatusScreen(),
        ),
        GoRoute(
          name: RouteNames.home,
          path: '/',
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          name: RouteNames.signIn,
          path: '/sign-in',
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          authControllerProvider.overrideWith(_ApprovedAuthController.new),
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
      find.byType(RegistrationStatusScreen),
      matchesGoldenFile('goldens/registration_status_1701-3789.png'),
    );
  });
}
