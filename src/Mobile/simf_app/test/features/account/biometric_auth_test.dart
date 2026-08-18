import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../support/simf_test_scope.dart';

/// A controllable [BiometricAuth] — `implements` ignores the real constructor
/// (which needs a Ref + local_auth), so the nudge and the toggle can be driven
/// without the device plugin. #7a — enrolment is no longer one-tap here; the
/// fake only needs the capability/enabled probes + the disable revoke.
class _FakeBiometricAuth implements BiometricAuth {
  _FakeBiometricAuth({
    this.available = true,
    this.enabled = false,
    // Part of the fake's surface even where a given test does not set it:
    // a fake that only exposes what is currently used has to be edited
    // every time a new case needs the other outcome.
    // ignore: unused_element_parameter
    this.confirmOutcome = LocalAuthOutcome.success,
  });

  bool available;
  bool enabled;
  LocalAuthOutcome confirmOutcome;
  int disableCalls = 0;
  int confirmCalls = 0;

  @override
  Future<bool> isAvailable() async => available;
  @override
  Future<bool> isEnabled() async => enabled;

  @override
  Future<LocalAuthOutcome> confirmDeviceIdentity(String reason) async {
    confirmCalls++;
    return confirmOutcome;
  }

  @override
  Future<void> disable() async {
    disableCalls++;
    enabled = false;
  }
}

/// A signed-in auth whose registration [status] drives the nudge guard (D-666):
/// the nudge is skipped for a guest — a not-yet-approved account presents as
/// guest via [CurrentUser.effectiveAppRole], so it has nothing to secure yet.
Session _session(RegistrationStatus status) => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: CurrentUser(
        id: 'u1',
        email: 'v@example.sa',
        displayName: 'Raed',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('ar'),
        registrationStatus: status,
      ),
    );

class _FakeAuth extends AuthController {
  _FakeAuth({this.status = RegistrationStatus.approved});

  final RegistrationStatus status;

  @override
  AuthState build() => AuthStateSignedIn(_session(status));
}

const String _nudgeBody =
    'Use your face or fingerprint to sign in next time — no password needed.';

/// Hosts [home] at `/` with a `biometric-step-up` route both the nudge and the
/// toggle navigate to — so the test asserts the launch by finding 'STEP-UP'.
Future<void> _pump(
  WidgetTester tester,
  Widget home, {
  required _FakeBiometricAuth biometric,
  AuthController? auth,
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(path: '/', builder: (c, s) => home),
      GoRoute(
        name: RouteNames.biometricStepUp,
        path: '/auth/biometric-step-up',
        builder: (c, s) => const Scaffold(body: Text('STEP-UP')),
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        biometricAuthProvider.overrideWithValue(biometric),
        // The nudge guard (D-666) reads the auth state; default to an approved
        // (non-guest) visitor so the availability tests behave as before.
        authControllerProvider.overrideWith(() => auth ?? _FakeAuth()),
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

/// A host with a button that fires the post-sign-in nudge.
Widget _nudgeHost() => Consumer(
      builder: (context, ref, _) => Scaffold(
        body: Center(
          child: ElevatedButton(
            onPressed: () => maybeOfferBiometricEnrolment(context, ref),
            child: const Text('GO'),
          ),
        ),
      ),
    );

void main() {
  group('maybeOfferBiometricEnrolment nudge (D-441 / D-445; #7a)', () {
    testWidgets(
        'available + not enabled → shows the nudge; tapping Enable '
        'routes to the step-up screen', (tester) async {
      final biometric = _FakeBiometricAuth();
      await _pump(tester, _nudgeHost(), biometric: biometric);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      // The notification-style nudge (a SnackBar with an Enable action).
      expect(find.text(_nudgeBody), findsOneWidget);

      // #7a — Enable no longer enrols one-tap; it routes to the OTP step-up
      // (the captured GoRouter is lifetime-safe after the originating screen).
      await tester.tap(find.text('Enable'));
      await tester.pumpAndSettle();
      expect(find.text('STEP-UP'), findsOneWidget);
    });

    testWidgets('already enabled → no nudge', (tester) async {
      final biometric = _FakeBiometricAuth(enabled: true);
      await _pump(tester, _nudgeHost(), biometric: biometric);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      expect(find.text(_nudgeBody), findsNothing);
    });

    testWidgets('biometrics unavailable → no nudge', (tester) async {
      final biometric = _FakeBiometricAuth(available: false);
      await _pump(tester, _nudgeHost(), biometric: biometric);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      expect(find.text(_nudgeBody), findsNothing);
    });

    testWidgets('guest (not-yet-approved account) → no nudge (D-666)',
        (tester) async {
      // A pending account presents as guest via effectiveAppRole, so the nudge
      // is skipped even though biometrics are available and not yet enabled.
      final biometric = _FakeBiometricAuth();
      await _pump(
        tester,
        _nudgeHost(),
        biometric: biometric,
        auth: _FakeAuth(status: RegistrationStatus.pending),
      );

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      expect(find.text(_nudgeBody), findsNothing);
    });
  });

  group('FaceIdToggleTile (D-445; #7a)', () {
    testWidgets('hidden when the device has no usable biometric',
        (tester) async {
      final biometric = _FakeBiometricAuth(available: false);
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );
      expect(find.byType(SwitchListTile), findsNothing);
    });

    testWidgets(
        'toggling on confirms intent, then routes to the step-up screen',
        (tester) async {
      final biometric = _FakeBiometricAuth();
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );

      final tile = find.byType(SwitchListTile);
      expect(tile, findsOneWidget);
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);

      await tester.tap(tile);
      await tester.pumpAndSettle();
      // The confirm gate — nothing happens until the user agrees.
      expect(find.text('Enable Face ID sign-in?'), findsOneWidget);
      expect(find.text('STEP-UP'), findsNothing);

      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(find.text('STEP-UP'), findsOneWidget);
    });

    testWidgets(
        'toggling off asks to confirm the permanent delete; confirming revokes',
        (tester) async {
      final biometric = _FakeBiometricAuth(enabled: true);
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );

      final tile = find.byType(SwitchListTile);
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);

      await tester.tap(tile);
      await tester.pumpAndSettle();
      // The destructive-action confirm — nothing is revoked until the user
      // agrees.
      expect(
        find.text('Your Face ID sign-in data will be permanently deleted '
            'from this device.'),
        findsOneWidget,
      );
      expect(biometric.disableCalls, 0);

      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();
      expect(biometric.disableCalls, 1);
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);
      await tester.pump(const Duration(seconds: 5));
    });

    testWidgets('cancelling the disable confirm keeps the device key',
        (tester) async {
      final biometric = _FakeBiometricAuth(enabled: true);
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );

      final tile = find.byType(SwitchListTile);
      await tester.tap(tile);
      await tester.pumpAndSettle();

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
      // No revoke, and the switch stays on (the key was never touched).
      expect(biometric.disableCalls, 0);
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);
    });
  });
}
