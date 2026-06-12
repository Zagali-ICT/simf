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

const String _worldMapAsset = 'assets/images/onboarding_world_map.jpg';
// D-373 — one looping, muted background video per step. The same hero clip
// ships as all three placeholders; the owner replaces 02/03 in place later.
const List<String> _videoAssets = <String>[
  'assets/videos/onboard_01.mp4',
  'assets/videos/onboard_02.mp4',
  'assets/videos/onboard_03.mp4',
];
// The design's photo treatment on step 1: #01132D at 90% over the image.
const Color _photoOverlay = Color(0xE601132D);
// Pill page dots (Figma 148:22): active beige, inactive soft gold at 50%.
const Color _dotInactive = Color(0x80D0AC77);

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
          if (_videoReady && _video != null)
            Positioned.fill(
              child: FittedBox(
                fit: BoxFit.cover,
                clipBehavior: Clip.hardEdge,
                child: SizedBox(
                  width: _video!.value.size.width,
                  height: _video!.value.size.height,
                  child: VideoPlayer(_video!),
                ),
              ),
            )
          else if (_index == 0)
            Positioned.fill(
              child: Image.asset(_worldMapAsset, fit: BoxFit.cover),
            ),
          const Positioned.fill(
            child: ColoredBox(color: _photoOverlay),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                // Back chevron on steps 2-3; a fixed-height slot keeps the
                // layout stable when it is absent on step 1.
                SizedBox(
                  height: 48,
                  child: _index > 0
                      ? Align(
                          alignment: Alignment.topLeft,
                          child: Padding(
                            padding: const EdgeInsets.only(left: 8, top: 8),
                            // The frame draws the chevron pointing left even
                            // in RTL; force LTR so it is not auto-mirrored.
                            child: IconButton(
                              onPressed: _onBack,
                              icon: const Icon(
                                Icons.arrow_back_ios_new,
                                color: Colors.white,
                                size: 20,
                                textDirection: TextDirection.ltr,
                              ),
                            ),
                          ),
                        )
                      : null,
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
                _Dots(count: _stepCount, activeIndex: _index),
                const Spacer(flex: 2),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      FilledButton(
                        onPressed: _onNext,
                        child: Text(
                          l10n.onboardingNext,
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                      // The skip link disappears on the last step (159:1052);
                      // the fixed-height slot keeps the button from jumping.
                      SizedBox(
                        height: 32,
                        child: isLast
                            ? null
                            : TextButton(
                                onPressed: _onSkip,
                                style: TextButton.styleFrom(
                                  foregroundColor: SimfTokens.accent,
                                ),
                                child: Text(
                                  l10n.onboardingSkip,
                                  style: const TextStyle(fontSize: 18),
                                ),
                              ),
                      ),
                    ],
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

/// The design's pill page dots — active 32×8 beige, inactive 16×8 soft gold.
/// Forced LTR so the active dot travels left → right exactly as in the
/// frames (which keep that progression even in the RTL design).
class _Dots extends StatelessWidget {
  const _Dots({required this.count, required this.activeIndex});

  final int count;
  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      textDirection: TextDirection.ltr,
      children: <Widget>[
        for (int i = 0; i < count; i++)
          AnimatedContainer(
            duration: const Duration(milliseconds: 200),
            margin: const EdgeInsets.symmetric(horizontal: 4),
            width: i == activeIndex ? 32 : 16,
            height: 8,
            decoration: BoxDecoration(
              color: i == activeIndex ? SimfTokens.beigeBorder : _dotInactive,
              borderRadius: BorderRadius.circular(999),
            ),
          ),
      ],
    );
  }
}
