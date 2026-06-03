import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/auth/sign_up_type_screen.dart';

Future<void> _pump(WidgetTester tester) async {
  final router = GoRouter(
    initialLocation: '/sign-up/type',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signUpType,
        path: '/sign-up/type',
        builder: (c, s) => const SignUpTypeScreen(),
      ),
      GoRoute(
        name: RouteNames.signUpForm,
        path: '/sign-up',
        builder: (c, s) =>
            Scaffold(body: Text('FORM type=${s.uri.queryParameters['type']}')),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
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

bool _continueEnabled(WidgetTester tester) {
  final button = tester.widget<FilledButton>(
    find.widgetWithText(FilledButton, 'Continue'),
  );
  return button.onPressed != null;
}

void main() {
  group('SignUpTypeScreen (Page 004)', () {
    testWidgets('initial: the three types render and Continue is disabled',
        (tester) async {
      await _pump(tester);

      expect(find.text('Visitor'), findsOneWidget);
      expect(find.text('Exhibitor'), findsOneWidget);
      expect(find.text('Sponsor'), findsOneWidget);
      expect(_continueEnabled(tester), isFalse);
    });

    testWidgets('selecting Visitor enables Continue and routes to the form '
        'carrying type=visitor', (tester) async {
      await _pump(tester);

      await tester.tap(find.text('Visitor'));
      await tester.pump();
      expect(_continueEnabled(tester), isTrue);

      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(find.text('FORM type=visitor'), findsOneWidget);
    });

    testWidgets('tapping a disabled type shows the CP-only note and never '
        'enables Continue', (tester) async {
      await _pump(tester);

      await tester.tap(find.text('Exhibitor'));
      await tester.pump();

      expect(find.byType(SnackBar), findsOneWidget);
      expect(_continueEnabled(tester), isFalse);
      expect(find.text('FORM type=visitor'), findsNothing);
    });

    testWidgets('the Sign in link leaves the sign-up flow', (tester) async {
      await _pump(tester);

      await tester.tap(find.widgetWithText(TextButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('SIGN-IN'), findsOneWidget);
    });
  });
}
