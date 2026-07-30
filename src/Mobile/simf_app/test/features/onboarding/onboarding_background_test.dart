import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/onboarding/widgets/onboarding_background.dart';
import 'package:video_player/video_player.dart';

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
Future<void> _pump(
  WidgetTester tester, {
  VideoPlayerController? video,
  bool videoReady = false,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: Stack(
          children: <Widget>[
            Positioned.fill(
              child: OnboardingBackground(video: video, videoReady: videoReady),
            ),
          ],
        ),
      ),
    ),
  );
  await tester.pump();
}

/// An initialised controller WITHOUT a platform decoder — the constructor is
/// inert (it only touches the platform from `initialize()`), and the
/// [VideoPlayer] widget paints an empty box while `playerId` is uninitialised.
/// That is enough to drive [OnboardingBackground]'s playing branch, which had
/// zero coverage: every other test in this repo ran the poster fallback
/// (DEF-ONB-005).
class _ReadyVideoController extends VideoPlayerController {
  _ReadyVideoController() : super.asset(AppAssets.onboardVideo);

  @override
  VideoPlayerValue get value => const VideoPlayerValue(
        duration: Duration(seconds: 12),
        size: Size(1920, 1080),
        isInitialized: true,
        isPlaying: true,
        isLooping: true,
        volume: 0,
      );
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

  group('OnboardingBackground — the PLAYING branch (DEF-ONB-005)', () {
    testWidgets('a ready controller paints the video over the poster, '
        'cover-fitted to its own frame size', (tester) async {
      final controller = _ReadyVideoController();
      addTearDown(controller.dispose);

      await _pump(tester, video: controller, videoReady: true);

      // The poster stays underneath (no navy flash while frames arrive)…
      expect(find.byType(Image), findsOneWidget);
      // …and the video is on top, cover-fitted at the clip's intrinsic size so
      // it fills the frame instead of letterboxing.
      expect(find.byType(VideoPlayer), findsOneWidget);
      final box = tester.widget<FittedBox>(
        find.descendant(
          of: find.byType(OnboardingBackground),
          matching: find.byType(FittedBox),
        ),
      );
      expect(box.fit, BoxFit.cover);
      final sized = tester.widget<SizedBox>(
        find.descendant(
          of: find.byType(FittedBox),
          matching: find.byType(SizedBox),
        ),
      );
      expect(sized.width, 1920);
      expect(sized.height, 1080);
    });

    testWidgets('the playing scrim drops to 60% so the footage is visible',
        (tester) async {
      final controller = _ReadyVideoController();
      addTearDown(controller.dispose);

      await _pump(tester, video: controller, videoReady: true);

      final scrim = tester.widget<ColoredBox>(
        find.descendant(
          of: find.byType(OnboardingBackground),
          matching: find.byType(ColoredBox),
        ),
      );
      expect(scrim.color, SimfTokens.navyFill60);
    });

    testWidgets('a controller that is not ready yet keeps the poster scrim',
        (tester) async {
      final controller = _ReadyVideoController();
      addTearDown(controller.dispose);

      // videoReady == false: the decoder exists but has produced no frames, so
      // the still poster (and its 90% scrim) must still own the screen.
      await _pump(tester, video: controller);

      expect(find.byType(VideoPlayer), findsNothing);
      final scrim = tester.widget<ColoredBox>(
        find.descendant(
          of: find.byType(OnboardingBackground),
          matching: find.byType(ColoredBox),
        ),
      );
      expect(scrim.color, SimfTokens.navyFill90);
    });
  });
}
