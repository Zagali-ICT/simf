import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/session/session_activity.dart';
import 'package:simf_app/core/session/session_timeout_overlay.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// D-726 / D-737 (owner item 11) — the app-side inactivity session guard.
/// It is an idle timer: while the signed-in user keeps interacting, the guard
/// silently keeps the ~5-min access token alive so they are never interrupted;
/// once they have been IDLE for [idleLimit] it shows a 30s "stay signed in /
/// sign out" countdown and signs them out only if it is ignored.
///
/// D-737 — the guard NEVER signs a user out without first showing that 30s
/// warning. The proactive keep-alive uses [AuthController.tryRefresh] (which
/// does not sign out on failure); if it cannot extend the session, the guard
/// falls back to the countdown instead of the old abrupt, unannounced sign-out
/// that made the app look like it "closed" mid-use.
///
/// The NCA 24h absolute session cap (D-443) is untouched: it is enforced
/// server-side, so once it is hit even "stay signed in" cannot extend and the
/// existing `SignedIn → SignedOut` router redirect in `app.dart` lands sign-in.
///
/// The countdown is an in-tree overlay (not a Navigator dialog), so it needs no
/// router navigator key. Durations + a [now] clock are injectable so the timing
/// is deterministic under test.
class SessionGuard extends ConsumerStatefulWidget {
  const SessionGuard({
    required this.child,
    this.tickInterval = const Duration(seconds: 15),
    this.warnLead = const Duration(seconds: 60),
    this.idleLimit = const Duration(minutes: 5),
    this.countdown = const Duration(seconds: 30),
    this.now,
    super.key,
  });

  final Widget child;

  /// How often the guard re-evaluates idle time + token expiry.
  final Duration tickInterval;

  /// Proactively keep the token alive once it is within this of expiring — but
  /// only while the user is still active (has not yet reached [idleLimit]).
  final Duration warnLead;

  /// How long the user may be inactive before the "stay signed in?" countdown
  /// appears. The owner-specified idle timeout: 5 min idle → warn → sign out.
  final Duration idleLimit;

  /// How long the "stay signed in?" countdown runs before auto sign-out.
  final Duration countdown;

  /// Injectable wall clock for tests; defaults to [DateTime.now].
  final DateTime Function()? now;

  @override
  ConsumerState<SessionGuard> createState() => _SessionGuardState();
}

class _SessionGuardState extends ConsumerState<SessionGuard>
    with WidgetsBindingObserver {
  Timer? _ticker;
  Timer? _countdownTimer;
  bool _warning = false;
  int _secondsLeft = 0;
  bool _refreshing = false;

  DateTime _now() => (widget.now ?? DateTime.now)();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _startTicker();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _ticker?.cancel();
    _countdownTimer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    // No point ticking while backgrounded; on resume re-check immediately (the
    // token may have lapsed while away — the cold-restore + 401 paths still
    // cover a session that fully expired in the background).
    if (state == AppLifecycleState.resumed) {
      _startTicker();
      _onTick();
    } else {
      _ticker?.cancel();
    }
  }

  void _startTicker() {
    _ticker?.cancel();
    _ticker = Timer.periodic(widget.tickInterval, (_) => _onTick());
  }

  void _onTick() {
    // Don't stack actions: skip while the warning is up or a refresh is
    // running.
    if (!mounted || _warning || _refreshing) {
      return;
    }
    final authState = ref.read(authControllerProvider);
    if (authState is! AuthStateSignedIn) {
      return;
    }
    final now = _now();
    final idleFor =
        now.difference(ref.read(sessionActivityProvider).lastActivity);
    // Inactive past the limit → warn, then sign out only if the 30s countdown
    // is ignored. Inactivity-driven and independent of the token, so a user who
    // is actively using the app is never interrupted.
    if (idleFor >= widget.idleLimit) {
      _startCountdown();
      return;
    }
    // Still active, but the access token is about to lapse → keep it alive in
    // the background so their next request never 401s. Uses tryRefresh (which
    // does NOT sign out on failure): if it cannot extend, we warn — never a
    // silent sign-out (D-737).
    final timeLeft = authState.session.accessTokenExpiresAt.difference(now);
    if (timeLeft <= widget.warnLead) {
      unawaited(_proactiveRefresh());
    }
  }

  Future<void> _proactiveRefresh() async {
    _refreshing = true;
    try {
      final extended =
          await ref.read(authControllerProvider.notifier).tryRefresh();
      // If the token could not be extended, fall back to the visible countdown
      // instead of an abrupt sign-out — the guard never closes the app
      // silently.
      if (!extended && mounted && !_warning) {
        _startCountdown();
      }
    } finally {
      _refreshing = false;
    }
  }

  void _startCountdown() {
    setState(() {
      _warning = true;
      _secondsLeft = widget.countdown.inSeconds;
    });
    _countdownTimer?.cancel();
    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      if (_secondsLeft <= 1) {
        timer.cancel();
        unawaited(_signOut()); // ran out → sign out
        return;
      }
      setState(() => _secondsLeft -= 1);
    });
  }

  Future<void> _onStaySignedIn() async {
    _countdownTimer?.cancel();
    ref.read(sessionActivityProvider).markActive();
    final extended =
        await ref.read(authControllerProvider.notifier).tryRefresh();
    if (!mounted) {
      return;
    }
    if (extended) {
      setState(() => _warning = false);
    } else {
      // Session genuinely can't be extended (past the 24h cap / revoked) — sign
      // out cleanly rather than leave a dead session behind the dismissed card.
      await _signOut();
    }
  }

  Future<void> _signOut() async {
    _countdownTimer?.cancel();
    if (mounted) {
      setState(() => _warning = false);
    }
    await ref.read(authControllerProvider.notifier).signOut();
  }

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: <Widget>[
        Listener(
          behavior: HitTestBehavior.translucent,
          // Any touch OR drag/scroll counts as activity, so reading a long list
          // by scrolling keeps the session alive (not just discrete taps).
          onPointerDown: (_) => ref.read(sessionActivityProvider).markActive(),
          onPointerMove: (_) => ref.read(sessionActivityProvider).markActive(),
          child: widget.child,
        ),
        if (_warning)
          SessionTimeoutOverlay(
            secondsLeft: _secondsLeft,
            onStaySignedIn: () => unawaited(_onStaySignedIn()),
            onSignOut: () => unawaited(_signOut()),
          ),
      ],
    );
  }
}
