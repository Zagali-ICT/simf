import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/onboarding/widgets/onboarding_background.dart';

/// Regression cover for the owner's 2026-07-26 report — "on the App onboarding
/// pages there is a video in the background — not exist / not working".
///
/// The clips DO ship (`assets/videos/onboard_01..03.mp4`, declared in
/// `pubspec.yaml`) and are muted / looping / autoplaying, but two defects made
/// the background read as empty:
///  1. the poster fallback was gated on `index == 0`, so a step whose decoder
///     failed (a device that refuses the codec, or the test runtime) showed a
///     BLANK navy screen on steps 2 and 3;
///  2. the video sat under the 90% photo overlay, leaving ~10% of the footage
///     visible — the motion read as "not there".
///
/// The decoder is never available under the test binding, so these tests lock
/// the fallback path: the poster is always painted and the still-poster scrim
/// stays at the design's 90%. The per-step half is pinned on the real screen in
/// `onboarding_screen_test.dart`.
Future<void> _pump(WidgetTester tester) async {
  await tester.pumpWidget(
    const MaterialApp(
      home: Scaffold(
        body: Stack(
          children: <Widget>[
            Positioned.fill(
              child: OnboardingBackground(video: null, videoReady: false),
            ),
          ],
        ),
      ),
    ),
  );
  await tester.pump();
}

void main() {
  group('OnboardingBackground (owner 2026-07-26 — background not visible)', () {
    testWidgets('always paints the world-map poster, covering the frame',
        (tester) async {
      await _pump(tester);

      final image = tester.widget<Image>(find.byType(Image));
      expect(image.image, isA<AssetImage>());
      expect(
        (image.image as AssetImage).assetName,
        AppAssets.onboardingWorldMap,
      );
      // The poster must cover the frame, never letterbox it.
      expect(image.fit, BoxFit.cover);
    });

    testWidgets('the still poster keeps the design 90% navy scrim',
        (tester) async {
      await _pump(tester);

      final scrim = tester.widget<ColoredBox>(
        find.descendant(
          of: find.byType(OnboardingBackground),
          matching: find.byType(ColoredBox),
        ),
      );
      expect(scrim.color, SimfTokens.navyFill90);
    });

    testWidgets('the video scrim is lighter than the poster scrim',
        (tester) async {
      // A playing video is scrimmed at 60% so the footage is actually visible;
      // the poster stays at the Figma 148:22 90%.
      expect(SimfTokens.navyFill60.a, lessThan(SimfTokens.navyFill90.a));
    });
  });
}
