import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/_legacy_mockup/onboarding_screen.dart';
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

void main() {
  group('OnboardingScreen (Page 002)', () {
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

    testWidgets('finishing the last slide completes onboarding',
        (tester) async {
      final prefs = _FakePrefs();
      await _pumpOnboarding(tester, prefs);

      // English locale: the button reads Next, Next, then Get started.
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      expect(prefs.getBool(StorageKeys.onboardingCompleted), isNull);

      await tester.tap(find.text('Get started'));
      await tester.pumpAndSettle();

      expect(prefs.getBool(StorageKeys.onboardingCompleted), isTrue);
      expect(find.text('SIGN-IN-SCREEN'), findsOneWidget);
    });

    testWidgets(
        'renders under the production theme without an infinite-width crash '
        '(D-295)', (tester) async {
      // Regression: the FilledButton theme sets minimumSize: Size.fromHeight(48)
      // (== Size(infinity, 48)). Inside the onboarding bottom Row that demanded
      // an infinite width and threw a layout assertion, collapsing the whole
      // body + nav buttons (observed on a real device). The other tests miss it
      // because they pump a bare MaterialApp with no theme; this one applies the
      // real SimfTheme so the regression is locked in.
      final router = GoRouter(
        initialLocation: '/onboarding',
        routes: <RouteBase>[
          GoRoute(
            name: RouteNames.onboarding,
            path: '/onboarding',
            builder: (context, state) => const OnboardingScreen(),
          ),
        ],
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: <Override>[
            simfPrefsStorageProvider.overrideWithValue(_FakePrefs()),
          ],
          child: MaterialApp.router(
            theme: SimfTheme.light(),
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

      expect(tester.takeException(), isNull);
      // The body + both nav buttons actually laid out (they did not before).
      expect(find.text('Next'), findsOneWidget);
      expect(find.text('Skip'), findsOneWidget);
    });
  });
}
