import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/live/widgets/live_video_player.dart';

/// D-721 — the live player is the one surface allowed to break the app-wide
/// portrait lock: landscape while the YouTube feed is fullscreen, portrait again
/// on exit (and on dispose, in case we're torn down mid-fullscreen). The pure
/// [liveFullScreenOrientations] helper is what the fullscreen listener + dispose
/// both drive, so locking it here locks the exception.
void main() {
  group('liveFullScreenOrientations (D-721 portrait-lock exception)', () {
    test('fullscreen → both landscape orientations, no portrait', () {
      expect(
        liveFullScreenOrientations(true),
        equals(const <DeviceOrientation>[
          DeviceOrientation.landscapeLeft,
          DeviceOrientation.landscapeRight,
        ]),
      );
    });

    test('not fullscreen → restores the portrait lock only', () {
      expect(
        liveFullScreenOrientations(false),
        equals(const <DeviceOrientation>[DeviceOrientation.portraitUp]),
      );
    });

    test('exiting fullscreen never leaves the device in landscape', () {
      expect(
        liveFullScreenOrientations(false),
        isNot(contains(DeviceOrientation.landscapeLeft)),
      );
      expect(
        liveFullScreenOrientations(false),
        isNot(contains(DeviceOrientation.landscapeRight)),
      );
    });
  });
}
