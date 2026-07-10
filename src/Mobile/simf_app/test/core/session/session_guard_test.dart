// D-726 (owner item 11, Model A) — behaviour tests for the app-side session
// auto-extend guard: silent refresh while active, a countdown overlay once idle,
// and auto sign-out if the countdown is ignored. A mutable `clock` drives the
// token expiry / idle maths; the widget timers are advanced with tester.pump.
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/session/session_activity.dart';
import 'package:simf_app/core/session/session_guard.dart';
import 'package:simf_app/core/session/session_timeout_overlay.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

final _t0 = DateTime.utc(2026, 1, 1, 12);

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'v@x.sa',
      displayName: 'Visitor One',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('en'),
      registrationStatus: RegistrationStatus.approved,
    );

/// A signed-in AuthController whose token expiry is fixed and whose refresh /
/// sign-out are recorded — the same `extends AuthController` + `build()` fake
/// pattern the live-screen tests use.
class _FakeAuth extends AuthController {
  _FakeAuth(this.expiry);

  final DateTime expiry;
  int refreshCalls = 0;
  int signOutCalls = 0;
  bool refreshSucceeds = true;

  @override
  AuthState build() => AuthStateSignedIn(_session(expiry));

  Session _session(DateTime exp) => Session(
        accessToken: 'A',
        refreshToken: 'R',
        accessTokenExpiresAt: exp,
        user: _visitor(),
      );

  @override
  Future<bool> refresh() async {
    refreshCalls++;
    if (!refreshSucceeds) {
      state = const AuthStateSignedOut();
      return false;
    }
    // A rotated token far in the future so no later tick acts again.
    state = AuthStateSignedIn(_session(expiry.add(const Duration(minutes: 30))));
    return true;
  }

  @override
  Future<void> signOut() async {
    signOutCalls++;
    state = const AuthStateSignedOut();
  }
}

void main() {
  late DateTime nowValue;
  DateTime clock() => nowValue;

  setUp(() => nowValue = _t0);

  Widget host(_FakeAuth auth, SessionActivity activity) => ProviderScope(
        overrides: <Override>[
          authControllerProvider.overrideWith(() => auth),
          sessionActivityProvider.overrideWithValue(activity),
        ],
        child: MaterialApp(
          locale: const Locale('en'),
          localizationsDelegates: AppL10n.localizationsDelegates,
          supportedLocales: AppL10n.supportedLocales,
          home: SessionGuard(
            // warnLead (60s) + activeWindow (4min) keep their defaults; only the
            // tick + countdown are shortened so the test pumps are quick.
            tickInterval: const Duration(seconds: 1),
            countdown: const Duration(seconds: 3),
            now: clock,
            child: const Scaffold(body: SizedBox.expand()),
          ),
        ),
      );

  testWidgets('near expiry + active → silent refresh, no overlay',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)));
    final activity = SessionActivity(now: clock); // lastActivity = t0 (active)
    await tester.pumpWidget(host(auth, activity));

    await tester.pump(); // settle the MaterialApp first frame
    await tester.pump(const Duration(seconds: 1)); // one guard tick
    await tester.pump(); // settle the async refresh

    expect(auth.refreshCalls, 1);
    expect(auth.signOutCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox()); // dispose → cancel timers
  });

  testWidgets('near expiry + idle → the countdown overlay appears',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)));
    final activity = SessionActivity(now: clock); // lastActivity = t0
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    nowValue = _t0.add(const Duration(minutes: 5)); // idle 5 min, token lapsed
    await tester.pump(const Duration(seconds: 1)); // one guard tick

    expect(find.byType(SessionTimeoutOverlay), findsOneWidget);
    expect(auth.refreshCalls, 0);

    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('idle overlay → "Stay signed in" refreshes and dismisses',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)));
    final activity = SessionActivity(now: clock);
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    nowValue = _t0.add(const Duration(minutes: 5));
    await tester.pump(const Duration(seconds: 1));
    expect(find.byType(SessionTimeoutOverlay), findsOneWidget);

    await tester.tap(find.text('Stay signed in'));
    await tester.pump(); // run _onStaySignedIn + refresh
    await tester.pump();

    expect(auth.refreshCalls, 1);
    expect(auth.signOutCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('idle overlay ignored → the countdown signs the user out',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)));
    final activity = SessionActivity(now: clock);
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    nowValue = _t0.add(const Duration(minutes: 5));
    await tester.pump(const Duration(seconds: 1)); // overlay appears, count = 3
    expect(find.byType(SessionTimeoutOverlay), findsOneWidget);

    // Let the 3s countdown run out (each pump also fires the guard tick, which
    // no-ops while the warning is up).
    await tester.pump(const Duration(seconds: 1));
    await tester.pump(const Duration(seconds: 1));
    await tester.pump(const Duration(seconds: 1));
    await tester.pump();

    expect(auth.signOutCalls, 1);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('token has comfortable life left → the guard does nothing',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(minutes: 5))); // far from expiry
    final activity = SessionActivity(now: clock);
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    await tester.pump(const Duration(seconds: 1));
    await tester.pump(const Duration(seconds: 1));

    expect(auth.refreshCalls, 0);
    expect(auth.signOutCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox());
  });
}
