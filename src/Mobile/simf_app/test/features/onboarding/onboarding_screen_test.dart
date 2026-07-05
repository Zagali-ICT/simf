import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/onboarding/onboarding_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// In-memory [SimfPrefsStorage] so the test needs no platform channel.
class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};

  @override
  bool? getBool(String key) {
    final value = _store[key];
    return value is bool ? value : null;
  }

  @override
  Future<bool> setBool(String key, bool value) async {
    _store[key] = value;
    return true;
  }

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

Future<void> _pumpOnboarding(WidgetTester tester, _FakePrefs prefs) async {
  final router = GoRouter(
    initialLocation: '/onboarding',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.onboarding,
        path: '/onboarding',
        builder: (context, state) => const OnboardingScreen(),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (context, state) =>
            const Scaffold(body: Text('SIGN-IN-SCREEN')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfPrefsStorageProvider.overrideWithValue(prefs),
        localeControllerProvider.overrideWith(() => LocaleController(prefs: prefs)),
      ],
      child: MaterialApp.router(
        theme: SimfTheme.dark(),
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

void main() {
  group('OnboardingScreen (Page 002 — KSA design, D-362)', () {
    testWidgets('Skip sets the first-run flag and routes to sign-in',
        (tester) async {
      final prefs = _FakePrefs();
      await _pumpOnboarding(tester, prefs);

      expect(find.text('SIGN-IN-SCREEN'), findsNothing);
      await tester.tap(find.text('Skip'));
      await tester.pumpAndSettle();

      expect(prefs.getBool(StorageKeys.onboardingCompleted), isTrue);
      expect(find.text('SIGN-IN-SCREEN'), findsOneWidget);
    });

    testWidgets('the third Next completes onboarding (no Get-started label)',
        (tester) async {
      final prefs = _FakePrefs();
      await _pumpOnboarding(tester, prefs);

      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      expect(prefs.getBool(StorageKeys.onboardingCompleted), isNull);

      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      expect(prefs.getBool(StorageKeys.onboardingCompleted), isTrue);
      expect(find.text('SIGN-IN-SCREEN'), findsOneWidget);
    });

    testWidgets('the skip link disappears on the last step', (tester) async {
      final prefs = _FakePrefs();
      await _pumpOnboarding(tester, prefs);

      expect(find.text('Skip'), findsOneWidget);
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      expect(find.text('Skip'), findsOneWidget);
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      expect(find.text('Skip'), findsNothing);
    });

    testWidgets('the language globe is present and stays tappable',
        (tester) async {
      final prefs = _FakePrefs();
      await _pumpOnboarding(tester, prefs);

      final toggle = find.byKey(const ValueKey<String>('languageToggle'));
      expect(toggle, findsOneWidget);
      await tester.tap(toggle);
      await tester.pumpAndSettle();
      // Toggling AR ↔ EN must not remove or crash the control.
      expect(toggle, findsOneWidget);
    });

    testWidgets('the back chevron steps back (hidden on step 1)',
        (tester) async {
      final prefs = _FakePrefs();
      await _pumpOnboarding(tester, prefs);

      expect(find.byIcon(Icons.arrow_back_ios_new), findsNothing);
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      expect(find.text('Skip'), findsNothing); // on the last step

      await tester.tap(find.byIcon(Icons.arrow_back_ios_new));
      await tester.pumpAndSettle();

      expect(find.text('Skip'), findsOneWidget); // back on step 2
    });
  });
}
