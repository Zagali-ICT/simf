@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/biometric_step_up_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the biometric step-up (enable Face-ID) screen. This is an
/// **unbound** auth screen — it reuses the shared KSA OTP frame (no dedicated
/// Figma node), so the golden is a render-regression lock rather than a parity
/// proof (D-554). Regenerate:
///   flutter test --update-goldens test/golden/biometric_step_up_golden_test.dart
///
/// Captured after the on-open step-up code request resolves (masked recipient
/// shown, countdown at its 01:00 start). The 1s resend timer means we pump
/// frames rather than settle.
class _FakeStepUpController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<String> sendBiometricStepUp() async => 'r***@xxx.sa';
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Biometric step-up @375x812 — KSA OTP frame (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/biometric-step-up',
      routes: <RouteBase>[
        GoRoute(
          path: '/biometric-step-up',
          builder: (_, __) => const BiometricStepUpScreen(),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          authControllerProvider.overrideWith(_FakeStepUpController.new),
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
    // initState fires the step-up send; pump frames (under 1s) to resolve it +
    // the SVG/asset futures while the countdown stays at its 01:00 start.
    await tester.pump();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 200));

    await expectLater(
      find.byType(BiometricStepUpScreen),
      matchesGoldenFile('goldens/biometric_step_up.png'),
    );
  });
}
