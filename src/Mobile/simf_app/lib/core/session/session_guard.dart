import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import 'session_activity.dart';
import 'session_timeout_overlay.dart';

/// D-726 (owner item 11, Model A) — the app-side session auto-extend guard.
/// Ports the shipped CP/Web session-timeout behaviour to Flutter: while a
/// signed-in user is ACTIVE, silently refresh the ~5-min access token before it
/// lapses (an active user is never interrupted); once the user is IDLE near
/// expiry, show a 30s "stay signed in / sign out" countdown and sign them out if
/// it is ignored.
///
/// The NCA 24h absolute session cap (D-443) is untouched: it is enforced
/// server-side, so a silent refresh simply fails past 24h and the existing
/// `SignedIn → SignedOut` router redirect in `app.dart` lands the sign-in screen.
///
/// Built entirely over the existing public `AuthController` API
/// ([AuthController.refresh] / [AuthController.signOut] /
/// `session.accessTokenExpiresAt`), so `simf_auth_pkg` is not changed. The
/// countdown is an in-tree overlay (not a Navigator dialog), so it needs no
/// router navigator key. Durations + a [now] clock are injectable so the timing
/// is deterministic under test.
class SessionGuard extends ConsumerStatefulWidget {
  const SessionGuard({
    required this.child,
    this.tickInterval = const Duration(seconds: 15),
    this.warnLead = const Duration(seconds: 60),
    this.activeWindow = const Duration(minutes: 4),
    this.countdown = const Duration(seconds: 30),
    this.now,
    super.key,
  });

  final Widget child;

  /// How often the guard checks the access token's time-to-expiry.
  final Duration tickInterval;

  /// Act (silent refresh, or warn) once the token is within this of expiring.
  final Duration warnLead;

  /// The user counts as "active" when they interacted within this window; past
  /// it they are "idle" and get the countdown instead of a silent refresh.
  final Duration activeWindow;

  /// How long the idle "stay signed in?" countdown runs before auto sign-out.
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
    // Don't stack actions: skip while the warning is up or a refresh is running.
    if (!mounted || _warning || _refreshing) {
      return;
    }
    final authState = ref.read(authControllerProvider);
    if (authState is! AuthStateSignedIn) {
      return;
    }
    final timeLeft =
        authState.session.accessTokenExpiresAt.difference(_now());
    if (timeLeft > widget.warnLead) {
      return; // the token still has comfortable life left
    }
    final idleFor =
        _now().difference(ref.read(sessionActivityProvider).lastActivity);
    if (idleFor <= widget.activeWindow) {
      unawaited(_silentRefresh()); // active → extend without interrupting
    } else {
      _startCountdown(); // idle → warn, then sign out if ignored
    }
  }

  Future<void> _silentRefresh() async {
    _refreshing = true;
    try {
      // A false result means the refresh failed (e.g. past the 24h cap): the
      // AuthController has already flipped to SignedOut and app.dart routes to
      // sign-in, so there is nothing more to do here.
      await ref.read(authControllerProvider.notifier).refresh();
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
    if (mounted) {
      setState(() => _warning = false);
    }
    ref.read(sessionActivityProvider).markActive();
    await _silentRefresh();
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
          onPointerDown: (_) => ref.read(sessionActivityProvider).markActive(),
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
