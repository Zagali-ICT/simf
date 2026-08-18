import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/registration/registration_success_screen.dart';

import '../../support/simf_test_scope.dart';

Future<void> _pump(
  WidgetTester tester, {
  String messageEn = 'Congratulations, welcome',
}) async {
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
    simfTestScope(
      overrides: <Override>[
        // D-461 — fixed site-settings so the screen never fires a real fetch.
        siteSettingsProvider.overrideWith(
          (ref) => SiteSettings(
            registrationMessageAr: 'تهانينا، مرحباً بكم',
            registrationMessageEn: messageEn,
            social: const SiteSocialLinks(),
          ),
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

    testWidgets('shows the CP-configured welcome message (D-461)',
        (tester) async {
      await _pump(tester, messageEn: 'Welcome to the Fourth Saudi Forum!');
      expect(find.text('Welcome to the Fourth Saudi Forum!'), findsOneWidget);
    });

    testWidgets('falls back to the bundled message when the CP value is empty',
        (tester) async {
      await _pump(tester, messageEn: '');
      // The (now-empty) configured value is not shown; the screen still renders
      // via the bundled l10n fallback.
      expect(find.text('Welcome to the Fourth Saudi Forum!'), findsNothing);
      expect(find.text('Registration success'), findsOneWidget);
    });
  });
}
