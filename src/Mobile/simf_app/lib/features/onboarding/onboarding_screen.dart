import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:video_player/video_player.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_logo.dart';
import 'widgets/onboarding_background.dart';
import 'widgets/onboarding_dots.dart';
import 'widgets/onboarding_top_bar.dart';

// D-373 — one looping, muted background video per step. The same hero clip
// ships as all three placeholders; the owner replaces 02/03 in place later.
const List<String> _videoAssets = <String>[
  'assets/videos/onboard_01.mp4',
  'assets/videos/onboard_02.mp4',
  'assets/videos/onboard_03.mp4',
];

/// Page 002 — التهيئة · Onboarding (first-run only). The KSA-Project Figma
/// design (frames 148:22 / 159:942 / 159:1052 — D-362): a three-step static
/// carousel — the world-map photo with a 90% navy overlay behind step 1, plain
/// navy behind steps 2–3 — with the brand mark, the welcome copy per step,
/// pill dots, the gold التالي button, a تخطي link (hidden on the last step)
/// and a back chevron (steps 2–3). Replaces the interim intro-video
/// placeholder per the owner's static-panels decision; the old screen is
/// parked in `_legacy_mockup/`.
///
/// Contract unchanged: finishing or skipping sets `onboardingCompleted` and
/// routes to sign-in; the splash gates on that flag. There is **no SIMF API**
/// (Page_002_API.md).
class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  static const int _stepCount = 3;

  final PageController _pageController = PageController();
  int _index = 0;

  // D-373 — one decoder at a time: the controller follows the active step.
  VideoPlayerController? _video;
  bool _videoReady = false;

  @override
  void initState() {
    super.initState();
    unawaited(_loadVideo(0));
  }

  /// Best-effort background video for the given step — a missing decoder
  /// (tests / unsupported runtime) silently falls back to the static
  /// image / navy background.
  Future<void> _loadVideo(int index) async {
    final old = _video;
    _video = null;
    _videoReady = false;
    if (mounted) {
      setState(() {});
    }
    await old?.dispose();
    final controller = VideoPlayerController.asset(_videoAssets[index]);
    try {
      await controller.initialize();
      await controller.setLooping(true);
      await controller.setVolume(0);
      await controller.play();
      if (!mounted || _index != index) {
        await controller.dispose();
        return;
      }
      setState(() {
        _video = controller;
        _videoReady = true;
      });
    } catch (_) {
      await controller.dispose();
    }
  }

  @override
  void dispose() {
    unawaited(_video?.dispose());
    _pageController.dispose();
    super.dispose();
  }

  /// Sets the first-run flag (Logic L-1) and routes to the sign-in entry.
  Future<void> _complete() async {
    await ref
        .read(simfPrefsStorageProvider)
        .setBool(StorageKeys.onboardingCompleted, true);
    if (mounted) {
      context.goNamed(RouteNames.signIn);
    }
  }

  void _onSkip() => unawaited(_complete());

  void _onNext() {
    if (_index >= _stepCount - 1) {
      unawaited(_complete());
      return;
    }
    unawaited(
      _pageController.nextPage(
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      ),
    );
  }

  void _onBack() {
    if (_index == 0) {
      return;
    }
    unawaited(
      _pageController.previousPage(
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final bodies = <String>[
      l10n.onboardingBody1,
      l10n.onboardingBody2,
      l10n.onboardingBody3,
    ];
    final isLast = _index == _stepCount - 1;

    return Scaffold(
      backgroundColor: SimfTokens.navy,
      body: Stack(
        children: <Widget>[
          // D-373 — every step plays its looping background video under the
          // navy overlay; until the decoder is ready (or when it is
          // unavailable) the step-1 world-map photo / plain navy shows.
          Positioned.fill(
            child: OnboardingBackground(
              video: _video,
              videoReady: _videoReady,
              index: _index,
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                OnboardingTopBar(
                  showBack: _index > 0,
                  onBack: _onBack,
                  showSkip: !isLast,
                  onSkip: _onSkip,
                  skipLabel: l10n.onboardingSkip,
                ),
                const Spacer(),
                const SimfLogo(size: 136),
                const SizedBox(height: 40),
                SizedBox(
                  height: 170,
                  child: PageView.builder(
                    controller: _pageController,
                    itemCount: _stepCount,
                    onPageChanged: (i) {
                      setState(() => _index = i);
                      // D-373 — swap the background video to the new step.
                      unawaited(_loadVideo(i));
                    },
                    itemBuilder: (context, i) => Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Text(
                            // The design repeats one welcome title on all
                            // three steps (148:22 / 159:943 / 159:1053).
                            l10n.onboardingTitle1,
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 24,
                              fontWeight: FontWeight.w600,
                              height: 1.5,
                            ),
                          ),
                          const SizedBox(height: 12),
                          Flexible(
                            child: Text(
                              bodies[i],
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: SimfTokens.beigeBorder,
                                fontSize: 18,
                                height: 1.5,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 24),
                OnboardingDots(count: _stepCount, activeIndex: _index),
                const Spacer(flex: 2),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  // The skip moved to the top-trailing corner; the bottom keeps
                  // only the primary التالي action.
                  child: FilledButton(
                    onPressed: _onNext,
                    child: Text(
                      l10n.onboardingNext,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 24),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
