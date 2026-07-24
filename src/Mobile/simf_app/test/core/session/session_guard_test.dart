// D-726 / D-737 (owner item 11) — behaviour tests for the app-side inactivity
// session guard: a proactive keep-alive while active, a 30s countdown overlay
// once idle, auto sign-out if the countdown is ignored, and — the D-737 fix —
// a fall-back to that same visible countdown (never a silent sign-out) when the
// keep-alive cannot extend the session. A mutable `clock` drives the token
// expiry / idle maths; the widget timers are advanced with tester.pump.
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

/// A signed-in AuthController whose token expiry is fixed and whose tryRefresh /
/// sign-out are recorded — the same `extends AuthController` + `build()` fake
/// pattern the live-screen tests use. [tryRefresh] mirrors the real contract:
/// on failure it returns false and does NOT sign out (that is the whole point of
/// D-737 — the guard, not the refresh, decides when to warn / sign out).
class _FakeAuth extends AuthController {
  _FakeAuth(this.expiry);

  final DateTime expiry;
  int tryRefreshCalls = 0;
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
  Future<bool> tryRefresh() async {
    tryRefreshCalls++;
    if (!refreshSucceeds) {
      return false; // D-737: tryRefresh never signs the user out on failure.
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
            // idleLimit (5min) + warnLead (60s) keep their defaults; only the
            // tick + countdown are shortened so the test pumps are quick.
            tickInterval: const Duration(seconds: 1),
            countdown: const Duration(seconds: 3),
            now: clock,
            child: const Scaffold(body: SizedBox.expand()),
          ),
        ),
      );

  testWidgets('active + token near expiry → proactive refresh, no overlay',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)));
    final activity = SessionActivity(now: clock); // lastActivity = t0 (active)
    await tester.pumpWidget(host(auth, activity));

    await tester.pump(); // settle the MaterialApp first frame
    await tester.pump(const Duration(seconds: 1)); // one guard tick
    await tester.pump(); // settle the async refresh

    expect(auth.tryRefreshCalls, 1);
    expect(auth.signOutCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox()); // dispose → cancel timers
  });

  testWidgets('inactive past the idle limit → the countdown overlay appears',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)));
    final activity = SessionActivity(now: clock); // lastActivity = t0
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    nowValue = _t0.add(const Duration(minutes: 5)); // idle 5 min
    await tester.pump(const Duration(seconds: 1)); // one guard tick

    expect(find.byType(SessionTimeoutOverlay), findsOneWidget);
    expect(auth.tryRefreshCalls, 0); // idle path never proactively refreshes

    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('D-737: keep-alive fails while active → warn, do NOT silently '
      'sign out', (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)))
      ..refreshSucceeds = false; // the refresh cannot extend the session
    final activity = SessionActivity(now: clock); // active (lastActivity = t0)
    await tester.pumpWidget(host(auth, activity));

    await tester.pump(); // settle the MaterialApp first frame
    await tester.pump(const Duration(seconds: 1)); // one guard tick → refresh
    await tester.pump(); // settle the async refresh → fall back to countdown

    expect(auth.tryRefreshCalls, 1);
    expect(auth.signOutCalls, 0); // the fix: no abrupt, unannounced sign-out
    expect(find.byType(SessionTimeoutOverlay), findsOneWidget);

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

    expect(auth.tryRefreshCalls, 1);
    expect(auth.signOutCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('idle overlay → "Stay signed in" but refresh fails → signs out',
      (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(seconds: 30)))
      ..refreshSucceeds = false;
    final activity = SessionActivity(now: clock);
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    nowValue = _t0.add(const Duration(minutes: 5));
    await tester.pump(const Duration(seconds: 1));
    expect(find.byType(SessionTimeoutOverlay), findsOneWidget);

    await tester.tap(find.text('Stay signed in'));
    await tester.pump(); // refresh returns false → sign out cleanly
    await tester.pump();

    expect(auth.tryRefreshCalls, 1);
    expect(auth.signOutCalls, 1);

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

  testWidgets('active + token has comfortable life left → the guard does '
      'nothing', (tester) async {
    final auth = _FakeAuth(_t0.add(const Duration(minutes: 5))); // far from expiry
    final activity = SessionActivity(now: clock);
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    await tester.pump(const Duration(seconds: 1));
    await tester.pump(const Duration(seconds: 1));

    expect(auth.tryRefreshCalls, 0);
    expect(auth.signOutCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox());
  });

  testWidgets(
      'D-726 (owner): watching a live stream never shows the extend-session '
      'overlay — the player keep-alive marks activity every 60s, always under '
      'the 5-min idle limit', (tester) async {
    // Token comfortably alive so the proactive-refresh path never fires; this
    // isolates the idle path (the "extend session?" overlay) the owner asked about.
    final auth = _FakeAuth(_t0.add(const Duration(hours: 1)));
    final activity = SessionActivity(now: clock); // lastActivity = t0 (the t=0 mark)
    await tester.pumpWidget(host(auth, activity));
    await tester.pump(); // settle the MaterialApp first frame

    // Simulate 10 minutes of PASSIVE watching (no taps/scrolls) — double the
    // 5-min idle limit. Step the clock 15s at a time; the real LiveVideoPlayer
    // keep-alive (D-726) marks activity every 60s, so idleFor climbs toward 60s
    // then resets on each heartbeat and never reaches idleLimit. The overlay must
    // NOT appear at ANY point during the watch.
    const totalSteps = 40; // 40 x 15s = 600s = 10 min
    for (var step = 1; step <= totalSteps; step++) {
      final elapsedSeconds = 15 * step;
      nowValue = _t0.add(Duration(seconds: elapsedSeconds));
      if (elapsedSeconds % 60 == 0) {
        activity.markActive(); // the live player's 60-second heartbeat
      }
      await tester.pump(const Duration(seconds: 1)); // one guard tick at this moment
      expect(find.byType(SessionTimeoutOverlay), findsNothing,
          reason: 'the extend-session overlay appeared at ${elapsedSeconds}s of watching');
    }

    // Never interrupted, never signed out across the whole watch.
    expect(auth.signOutCalls, 0);
    expect(auth.tryRefreshCalls, 0);
    expect(find.byType(SessionTimeoutOverlay), findsNothing);

    await tester.pumpWidget(const SizedBox());
  });
}
