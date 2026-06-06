import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/guest/guest_mode_screen.dart';

Future<void> _pump(WidgetTester tester) async {
  final router = GoRouter(
    initialLocation: '/guest',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.guestMode,
        path: '/guest',
        builder: (c, s) => const GuestModeScreen(),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN IN')),
      ),
    ],
  );

  await tester.pumpWidget(
    MaterialApp.router(
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
  );
  await tester.pumpAndSettle();
}

void main() {
  group('GuestModeScreen (Page 012)', () {
    testWidgets('renders the headline + both actions', (tester) async {
      await _pump(tester);

      expect(find.text('Browsing as guest'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Continue as guest'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Sign in'), findsOneWidget);
    });

    testWidgets('primary button continues to home', (tester) async {
      await _pump(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Continue as guest'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
    });

    testWidgets('secondary button routes to sign-in', (tester) async {
      await _pump(tester);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('SIGN IN'), findsOneWidget);
    });
  });
}
