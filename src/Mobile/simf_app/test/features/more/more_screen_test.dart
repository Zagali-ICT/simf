import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/more/more_screen.dart';

GoRouter _router() {
  return GoRouter(
    initialLocation: '/more',
    routes: <RouteBase>[
      GoRoute(
        path: '/more',
        builder: (context, state) => const MoreScreen(),
      ),
      GoRoute(
        name: RouteNames.aboutForum,
        path: '/about',
        builder: (context, state) => const Scaffold(
          body: Center(child: Text('ABOUT-MARKER')),
        ),
      ),
    ],
  );
}

Future<void> _pump(WidgetTester tester, {required GoRouter router}) async {
  await tester.pumpWidget(
    ProviderScope(
      child: MaterialApp.router(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        routerConfig: router,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MoreScreen (Page 041)', () {
    testWidgets('renders the navigation tiles', (tester) async {
      await _pump(tester, router: _router());
      expect(find.text('About the forum'), findsOneWidget);
      expect(find.text('Accessibility'), findsOneWidget);
      expect(find.text('Terms & conditions'), findsOneWidget);
      expect(find.text('Rate'), findsOneWidget);
      expect(find.text('Notifications'), findsOneWidget);
      expect(find.text('Media partners'), findsOneWidget);
    });

    testWidgets('shows the app-version line', (tester) async {
      await _pump(tester, router: _router());
      expect(find.text('SIMF v0.1.0'), findsOneWidget);
    });

    testWidgets('tapping About navigates to the About route', (tester) async {
      await _pump(tester, router: _router());
      await tester.tap(find.text('About the forum'));
      await tester.pumpAndSettle();
      expect(find.text('ABOUT-MARKER'), findsOneWidget);
    });
  });
}
