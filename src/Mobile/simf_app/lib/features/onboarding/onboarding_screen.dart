import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:video_player/video_player.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/app_assets.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_logo.dart';
import '../../core/motion/motion_durations.dart';
import 'widgets/onboarding_background.dart';
import 'widgets/onboarding_dots.dart';
import 'widgets/onboarding_top_bar.dart';

/// Page 002 — التهيئة · Onboarding (first-run only). The KSA-Project Figma
/// design (frames 148:22 / 159:942 / 159:1052 — D-362): a three-step carousel
/// over one looping, muted hero clip (world-map poster fallback) — with the
/// brand mark, the step's own title + body, pill dots, the gold التالي button,
/// a تخطي link (hidden on the last step) and a back chevron (steps 2–3). The
/// old screen is parked in `_legacy_mockup/`.
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

  // D-373 — ONE decoder for the whole carousel. The clip is the same on every
  // step, so it is opened once in initState and simply keeps playing across
  // swipes (DEF-ONB-004: re-initialising per step tore the background down for
  // ~a second and restarted the footage at 0:00 on every swipe).
  VideoPlayerController? _video;
  bool _videoReady = false;

  @override
  void initState() {
    super.initState();
    unawaited(_loadVideo());
  }

  /// Best-effort background video — a missing decoder (tests / unsupported
  /// runtime) falls back to the world-map poster that [OnboardingBackground]
  /// always paints underneath.
  ///
  /// Owner 2026-07-26 — the failure was swallowed by a bare `catch (_)`, so a
  /// device that refuses the clip looked identical to one that never had it.
  /// The reason is now printed in debug builds (release stays silent: a
  /// decorative background must never surface an error to a visitor).
  Future<void> _loadVideo() async {
    const asset = AppAssets.onboardVideo;
    final controller = VideoPlayerController.asset(asset);
    try {
      await controller.initialize();
      await controller.setLooping(true);
      await controller.setVolume(0);
      await controller.play();
      if (!mounted) {
        await controller.dispose();
        return;
      }
      setState(() {
        _video = controller;
        _videoReady = true;
      });
    } catch (error) {
      debugPrint('Onboarding background video "$asset" failed to play: $error');
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

  /// The globe toggles AR ↔ EN and persists the choice (D-363), matching the
  /// sign-in language control.
  void _toggleLanguage() {
    final isArabic = ref.read(localeControllerProvider).languageCode == 'ar';
    unawaited(
      ref
          .read(localeControllerProvider.notifier)
          .setLanguage(isArabic ? 'en' : 'ar'),
    );
  }

  void _onNext() {
    if (_index >= _stepCount - 1) {
      unawaited(_complete());
      return;
    }
    unawaited(
      _pageController.nextPage(
        duration: MotionDurations.dotFade,
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
        duration: MotionDurations.dotFade,
        curve: Curves.easeOut,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // DEF-ONB-006 — each step has its OWN title; the screen used to render
    // title 1 three times while onboardingTitle2/3 sat unused, so every step
    // read as the same welcome panel.
    final titles = <String>[
      l10n.onboardingTitle1,
      l10n.onboardingTitle2,
      l10n.onboardingTitle3,
    ];
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
          // D-373 — the looping background clip runs under the navy scrim for
          // the whole carousel; until the decoder is ready (or when it never
          // becomes ready) the world-map poster underneath shows on EVERY step.
          Positioned.fill(
            child: OnboardingBackground(
              video: _video,
              videoReady: _videoReady,
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                OnboardingTopBar(
                  showBack: _index > 0,
                  onBack: _onBack,
                  onToggleLanguage: _toggleLanguage,
                ),
                const SizedBox(height: SimfTokens.space2),
                const SimfLogo(size: 136),
                const SizedBox(height: SimfTokens.space10),
                SizedBox(
                  height: SimfTokens.onboardCarouselHeight,
                  child: PageView.builder(
                    controller: _pageController,
                    itemCount: _stepCount,
                    onPageChanged: (i) => setState(() => _index = i),
                    itemBuilder: (context, i) => Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: SimfTokens.space6,
                      ),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Text(
                            // Per-step title (148:22 / 159:943 / 159:1053).
                            titles[i],
                            textAlign: TextAlign.center,
                            style: SimfTokens.labelWhiteSemibold24Tall,
                          ),
                          const SizedBox(height: SimfTokens.space3),
                          Flexible(
                            child: Text(
                              bodies[i],
                              textAlign: TextAlign.center,
                              style: SimfTokens.bodyBeigeTitleTall,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: SimfTokens.space6),
                OnboardingDots(count: _stepCount, activeIndex: _index),
                const Spacer(),
                Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space4,
                  ),
                  child: FilledButton(
                    onPressed: _onNext,
                    child: Text(
                      l10n.onboardingNext,
                      style: SimfTokens.titleBold,
                    ),
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                // تخطي sits under the primary action, centered, on every step
                // but the last — Figma 758:1077 (node 758:1091). A matching
                // spacer holds the last step steady when the link is gone.
                if (isLast)
                  const SizedBox(height: SimfTokens.controlHeight)
                else
                  TextButton(
                    onPressed: _onSkip,
                    style: TextButton.styleFrom(
                      foregroundColor: SimfTokens.accent,
                    ),
                    child: Text(
                      l10n.onboardingSkip,
                      style: SimfTokens.titleSemibold,
                    ),
                  ),
                const SizedBox(height: SimfTokens.space4),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
