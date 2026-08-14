import 'package:flutter/cupertino.dart' show Listener;
import 'package:flutter/material.dart' show Listener;
import 'package:flutter/widgets.dart' show Listener;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/session/session_guard.dart' show SessionGuard;

/// D-726 (owner item 11) — tracks the last moment the user did something the
/// [SessionGuard] should treat as activity: a screen touch (wired app-wide in
/// `app.dart`) or an explicit keep-alive from a passive-but-engaged surface
/// such as the live video screen (#13 — "watching the stream does not log you
/// out").
///
/// The guard reads [lastActivity] on its periodic tick; nothing rebuilds when
/// it is marked, so this is a plain object behind a [Provider], not a Notifier.
/// A `now` hook keeps it deterministic in tests.
///
/// Known limitation (accepted, low): the app-wide signal is a pointer-down
/// [Listener], so a user who ONLY types on the soft keyboard for longer than
/// the idle window (no taps / scrolls) is seen as idle and gets the countdown.
/// A screen tap focuses the field first, so it is a narrow edge, the countdown
/// is a visible one-tap "stay signed in", and hooking the platform text-input
/// to mark activity is disproportionate for the risk — so it is documented, not
/// built.
class SessionActivity {
  SessionActivity({DateTime Function()? now}) : _now = now ?? DateTime.now {
    _lastActivity = _now();
  }

  final DateTime Function() _now;
  late DateTime _lastActivity;

  DateTime get lastActivity => _lastActivity;

  /// Record activity "now". Cheap (a single field write) — safe to call on
  /// every pointer-down and on a passive-watch heartbeat.
  void markActive() => _lastActivity = _now();
}

/// App-wide activity clock read by the [SessionGuard].
final sessionActivityProvider =
    Provider<SessionActivity>((ref) => SessionActivity());
