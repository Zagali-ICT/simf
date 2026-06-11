import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/registration/registration_success_screen.dart';

Future<void> _pump(WidgetTester tester) async {
  final router = GoRouter(
    initialLocation: '/registration/success',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.registrationSuccess,
        path: '/registration/success',
        builder: (c, s) => const RegistrationSuccessScreen(),
      ),
      GoRoute(
        name: RouteNames.registrationStatus,
        path: '/registration/status',
        builder: (c, s) => const Scaffold(body: Text('STATUS')),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
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
  group('RegistrationSuccessScreen (Page 010)', () {
    testWidgets('renders the confirmation + both actions', (tester) async {
      await _pump(tester);

      expect(find.text('Registration success'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Registration status'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Go to home'), findsOneWidget);
    });

    testWidgets('primary button routes to the registration-status screen',
        (tester) async {
      await _pump(tester);

      await tester.tap(
        find.widgetWithText(FilledButton, 'Registration status'),
      );
      await tester.pumpAndSettle();

      expect(find.text('STATUS'), findsOneWidget);
    });

    testWidgets('ghost button routes home', (tester) async {
      await _pump(tester);

      await tester.ensureVisible(
        find.widgetWithText(OutlinedButton, 'Go to home'),
      );
      await tester.tap(find.widgetWithText(OutlinedButton, 'Go to home'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
    });
  });
}
