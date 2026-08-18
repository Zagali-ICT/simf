import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/session/session_activity.dart';
import 'package:simf_app/features/live/widgets/live_video_player.dart';

import '../../support/simf_test_scope.dart';

/// D-721 — the live player is the one surface allowed to break the app-wide
/// portrait lock: landscape while the YouTube feed is fullscreen, portrait
/// again on exit (and on dispose, in case we're torn down mid-fullscreen). The
/// pure [liveFullScreenOrientations] helper is what the fullscreen listener +
/// dispose both drive, so locking it here locks the exception.
///
/// D-726 (#27) — the watch keep-alive now lives on this shared player (moved
/// off `LivePlayerSurface`) so the summary recording / summary-video surfaces
/// that use it directly are also kept active. The second group locks that:
/// mount marks activity once, and each 60-second tick re-marks it.
void main() {
  group('liveFullScreenOrientations (D-721 portrait-lock exception)', () {
    test('fullscreen → both landscape orientations, no portrait', () {
      expect(
        liveFullScreenOrientations(isFullScreen: true),
        equals(const <DeviceOrientation>[
          DeviceOrientation.landscapeLeft,
          DeviceOrientation.landscapeRight,
        ]),
      );
    });

    test('not fullscreen → restores the portrait lock only', () {
      expect(
        liveFullScreenOrientations(isFullScreen: false),
        equals(const <DeviceOrientation>[DeviceOrientation.portraitUp]),
      );
    });

    test('exiting fullscreen never leaves the device in landscape', () {
      expect(
        liveFullScreenOrientations(isFullScreen: false),
        isNot(contains(DeviceOrientation.landscapeLeft)),
      );
      expect(
        liveFullScreenOrientations(isFullScreen: false),
        isNot(contains(DeviceOrientation.landscapeRight)),
      );
    });
  });

  group('watch keep-alive (D-726 #27 — moved into the shared player)', () {
    // An HLS URL: not a YouTube id, so the player takes the video_player path
    // whose platform call is unimplemented in a headless test and degrades to
    // the error surface. That is irrelevant here — the keep-alive runs in
    // initState / a periodic Timer regardless of whether the feed ever plays.
    Widget host(SessionActivity activity) => simfTestScope(
          overrides: <Override>[
            sessionActivityProvider.overrideWithValue(activity),
          ],
          child: const MaterialApp(
            locale: Locale('en'),
            localizationsDelegates: AppL10n.localizationsDelegates,
            supportedLocales: AppL10n.supportedLocales,
            home: Scaffold(
              body: LiveVideoPlayer(url: 'https://example.com/stream.m3u8'),
            ),
          ),
        );

    testWidgets('mounting the player marks the session active once',
        (tester) async {
      final activity = _CountingActivity();
      await tester.pumpWidget(host(activity));
      await tester.pump(); // settle the async feed bind

      expect(activity.marks, 1);

      await tester.pumpWidget(const SizedBox()); // dispose → cancel the timer
    });

    testWidgets('a 60-second tick re-marks the session active', (tester) async {
      final activity = _CountingActivity();
      await tester.pumpWidget(host(activity));
      await tester.pump(); // mount → marks == 1

      await tester.pump(const Duration(minutes: 1)); // one periodic tick

      expect(activity.marks, 2);

      await tester.pumpWidget(const SizedBox()); // dispose → cancel the timer
    });

    testWidgets('leaving the screen cancels the keep-alive (no further marks)',
        (tester) async {
      final activity = _CountingActivity();
      await tester.pumpWidget(host(activity));
      await tester.pump(); // mount → marks == 1

      await tester.pumpWidget(const SizedBox()); // dispose cancels the timer
      await tester.pump(const Duration(minutes: 5)); // no tick can fire now

      expect(activity.marks, 1);
    });
  });
}

/// Counts [markActive] calls so a test can assert the keep-alive fired. The
/// base constructor seeds `lastActivity` directly (not via [markActive]), so
/// [marks] starts at 0.
class _CountingActivity extends SessionActivity {
  int marks = 0;

  @override
  void markActive() => marks++;
}
