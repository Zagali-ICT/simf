@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/sign_up_form_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the sign-up form against Figma frame **168:3454**
/// (إنشاء حساب · Page 005). Regenerate:
///   flutter test --update-goldens test/golden/sign_up_form_golden_test.dart
///
/// Frame parity: the navy scaffold + sweep, the shared back/globe top controls,
/// the forum logo + name header, and the beige card (title, email + password +
/// confirm-password fields with the SVG eye glyph, the gold إنشاء حساب CTA, and
/// the "have account? sign in" foot). Captured in the empty/default state — what
/// this locks is the layout, typography, colour, spacing and RTL of the
/// clean-code-frozen page (D-551).
class _StaticAuthController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final value = _store[key];
    return value is String ? value : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) => null;
  @override
  Future<bool> setBool(String key, bool value) async => true;
  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Sign-up form @375x812 — Figma 168:3454 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final prefs = _FakePrefs();
    final router = GoRouter(
      initialLocation: '/sign-up',
      routes: <RouteBase>[
        GoRoute(
          path: '/sign-up',
          name: RouteNames.signUpForm,
          builder: (_, __) => const SignUpFormScreen(),
        ),
        GoRoute(
          path: '/sign-in',
          name: RouteNames.signIn,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/auth/email-otp',
          name: RouteNames.emailOtp,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          simfPrefsStorageProvider.overrideWithValue(prefs),
          authControllerProvider.overrideWith(_StaticAuthController.new),
          localeControllerProvider.overrideWith(
            () => LocaleController(prefs: prefs),
          ),
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
      find.byType(SignUpFormScreen),
      matchesGoldenFile('goldens/sign_up_form_168-3454.png'),
    );
  });
}
