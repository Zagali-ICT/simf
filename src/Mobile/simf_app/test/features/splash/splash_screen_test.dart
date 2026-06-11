import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_logo.dart';
import 'package:simf_app/features/splash/splash_controller.dart';
import 'package:simf_app/features/splash/splash_screen.dart';

/// Replaces the real boot sequence with a fixed state so the screen's render
/// + one-shot route-out glue is tested in isolation (the sequence itself is
/// covered by splash_controller_test.dart).
class _StubSplashController extends SplashController {
  _StubSplashController(this._fixed);

  final SplashState _fixed;

  @override
  SplashState build() => _fixed;
}

Future<void> _pump(WidgetTester tester, SplashState state) async {
  final router = GoRouter(
    initialLocation: '/splash',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.splash,
        path: '/splash',
        builder: (c, s) => const SplashScreen(),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        splashControllerProvider.overrideWith(
          () => _StubSplashController(state),
        ),
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
  await tester.pump();
}

void main() {
  group('SplashScreen (Page 001 — KSA design, D-361)', () {
    testWidgets('renders the brand lock-up while booting', (tester) async {
      await _pump(tester, const SplashLoading());

      expect(find.byType(SimfLogo), findsOneWidget);
      expect(find.text('SAUDI · MOD · RSNF'), findsOneWidget);
      expect(find.text('Saudi International Maritime Forum'), findsOneWidget);
      expect(find.text('4th Edition\n23–25 Nov 2026 · Riyadh'), findsOneWidget);
    });

    testWidgets('routes out once the boot resolves to a route name',
        (tester) async {
      await _pump(
        tester,
        const SplashReady(routeName: RouteNames.signIn),
      );
      await tester.pumpAndSettle();

      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('routes out to a resumed location', (tester) async {
      await _pump(tester, const SplashReady(location: '/'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
    });
  });
}
