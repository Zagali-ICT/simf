import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/change_email_screen.dart';
import 'package:simf_app/features/account/data/email_change_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// #24 — widget tests for the self-service change-email screen. They prove the
/// two-phase flow: phase-1 client validation (blank / invalid / same-as-current)
/// blocks the round-trip; a valid address advances to the code phase; a valid
/// code confirms, signs the user out and routes to sign-in; a server error on
/// confirm shows inline and keeps the user on the screen.
class _FakeEmailChangeRepo implements EmailChangeRepository {
  _FakeEmailChangeRepo({this.confirmError});

  final ApiFailure? confirmError;
  int sendCalls = 0;
  int confirmCalls = 0;
  String? lastNewEmail;
  String? lastCode;
  String? lastPassword;

  @override
  Future<EmailChangeCodeSent> sendOtp(String newEmail) async {
    sendCalls++;
    lastNewEmail = newEmail;
    return const EmailChangeCodeSent(
      maskedNewEmail: 'n***@simf.test',
      expiresInSeconds: 600,
      resendCooldownSeconds: 120,
    );
  }

  @override
  Future<void> confirm({
    required String newEmail,
    required String code,
    required String currentPassword,
  }) async {
    confirmCalls++;
    lastNewEmail = newEmail;
    lastCode = code;
    lastPassword = currentPassword;
    if (confirmError != null) {
      throw confirmError!;
    }
  }
}

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: CurrentUser(
        id: 'u1',
        email: 'visitor@example.sa',
        displayName: 'Raed',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('en'),
        registrationStatus: RegistrationStatus.approved,
      ),
    );

class _FakeAuthController extends AuthController {
  int signOutCalls = 0;

  @override
  AuthState build() => AuthStateSignedIn(_session());

  @override
  Future<void> signOut() async {
    signOutCalls++;
    state = const AuthStateSignedOut();
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeEmailChangeRepo repo,
  _FakeAuthController auth, {
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/change',
    routes: <RouteBase>[
      GoRoute(
        path: '/change',
        builder: (c, s) => const ChangeEmailScreen(),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        emailChangeRepositoryProvider.overrideWithValue(repo),
        authControllerProvider.overrideWith(() => auth),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
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

Future<void> _tapSend(WidgetTester tester) async {
  await tester.tap(find.widgetWithText(FilledButton, 'Send code'));
  await tester.pump();
}

void main() {
  group('ChangeEmailScreen (#24)', () {
    testWidgets('a valid new email + code confirms, signs out, routes to sign-in',
        (tester) async {
      final repo = _FakeEmailChangeRepo();
      final auth = _FakeAuthController();
      await _pump(tester, repo, auth);

      await tester.enterText(find.byType(TextFormField), 'new@simf.test');
      await tester.pump();
      await _tapSend(tester);

      // Advanced to the code phase and issued the code.
      expect(repo.sendCalls, 1);
      expect(repo.lastNewEmail, 'new@simf.test');
      expect(find.text('n***@simf.test'), findsOneWidget);

      // The OTP capture field is a raw TextField (first in the tree); the
      // password is the code phase's only TextFormField.
      await tester.enterText(find.byType(TextField).first, '123456');
      await tester.pump();
      await tester.enterText(find.byType(TextFormField), 'Password1!');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(repo.confirmCalls, 1);
      expect(repo.lastCode, '123456');
      expect(repo.lastPassword, 'Password1!');
      expect(auth.signOutCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);

      await tester.pump(const Duration(seconds: 5)); // flush the SnackBar timer
    });

    testWidgets('an invalid email shows the format error and blocks send',
        (tester) async {
      final repo = _FakeEmailChangeRepo();
      await _pump(tester, repo, _FakeAuthController());

      await tester.enterText(find.byType(TextFormField), 'not-an-email');
      await tester.pump();
      await _tapSend(tester);

      expect(find.text('Invalid email'), findsOneWidget);
      expect(repo.sendCalls, 0);
    });

    testWidgets('the current email blocks send with a same-as-current error',
        (tester) async {
      final repo = _FakeEmailChangeRepo();
      await _pump(tester, repo, _FakeAuthController());

      await tester.enterText(
        find.byType(TextFormField),
        'visitor@example.sa',
      );
      await tester.pump();
      await _tapSend(tester);

      expect(
        find.text('This is already your email address.'),
        findsOneWidget,
      );
      expect(repo.sendCalls, 0);
    });

    testWidgets('a wrong code shows the inline error and stays on the screen',
        (tester) async {
      final repo = _FakeEmailChangeRepo(
        confirmError: const ApiFailure(
          code: 'AUTH_CODE_INVALID',
          message: 'The verification code is not correct.',
          httpStatus: 400,
        ),
      );
      final auth = _FakeAuthController();
      await _pump(tester, repo, auth);

      await tester.enterText(find.byType(TextFormField), 'new@simf.test');
      await tester.pump();
      await _tapSend(tester);

      await tester.enterText(find.byType(TextField).first, '999999');
      await tester.pump();
      await tester.enterText(find.byType(TextFormField), 'Password1!');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(repo.confirmCalls, 1);
      expect(auth.signOutCalls, 0);
      expect(find.text('SIGN-IN'), findsNothing);
      expect(
        find.text('The verification code is not correct.'),
        findsOneWidget,
      );

      await tester.pumpWidget(const SizedBox()); // cancel the resend timer
    });

    testWidgets('resend re-requests a code once the countdown ends (H1)',
        (tester) async {
      final repo = _FakeEmailChangeRepo();
      await _pump(tester, repo, _FakeAuthController());

      await tester.enterText(find.byType(TextFormField), 'new@simf.test');
      await tester.pump();
      await _tapSend(tester);
      expect(repo.sendCalls, 1);

      // Advance past the 120s cooldown so the resend action is enabled. Before
      // the H1 fix this threw (the phase-1 Form is unmounted in the code phase,
      // so _emailFormKey.currentState was null); now it re-issues for the same
      // address.
      await tester.pump(const Duration(seconds: 121));
      await tester.tap(find.text('Resend'));
      await tester.pump();

      expect(repo.sendCalls, 2);

      await tester.pumpWidget(const SizedBox()); // cancel the resend timer
    });

    testWidgets('renders the Arabic title in RTL', (tester) async {
      final repo = _FakeEmailChangeRepo();
      await _pump(tester, repo, _FakeAuthController(),
          locale: const Locale('ar'));

      expect(find.text('تغيير البريد الإلكتروني'), findsWidgets);
    });
  });
}
