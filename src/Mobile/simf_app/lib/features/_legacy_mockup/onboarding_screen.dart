import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// LEGACY — the pre-redesign intro-video placeholder Page 002 onboarding,
/// parked here when the KSA-Project static-panel design landed at the real
/// route (owner decision, D-362). Never routed; kept compiling until the owner
/// approves deleting the legacy directory at programme close (§6 freeze rules).
class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  static const int _slideCount = 3;

  final PageController _pageController = PageController();
  int _index = 0;

  @override
  void dispose() {
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
    if (_index >= _slideCount - 1) {
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final titles = <String>[
      l10n.onboardingTitle1,
      l10n.onboardingTitle2,
      l10n.onboardingTitle3,
    ];
    final isLast = _index == _slideCount - 1;

    return Scaffold(
      backgroundColor: SimfTokens.navy,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            children: <Widget>[
              _ProgressSegments(count: _slideCount, activeIndex: _index),
              Expanded(
                child: PageView.builder(
                  controller: _pageController,
                  itemCount: _slideCount,
                  onPageChanged: (i) => setState(() => _index = i),
                  itemBuilder: (context, i) => _IntroVideoSlide(
                    index: i,
                    title: titles[i],
                    videoName: 'introd_00${i + 1}',
                  ),
                ),
              ),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  TextButton(
                    onPressed: _onSkip,
                    child: Text(
                      l10n.onboardingSkip,
                      style: const TextStyle(
                        color: SimfTokens.txtTertiary,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ),
                  FilledButton(
                    // The app's FilledButton theme sets a full-width minimum
                    // (minimumSize: Size.fromHeight(48) == Size(infinity, 48)).
                    // In a Column that harmlessly stretches to fill, but inside
                    // this Row it demands an infinite width and throws a layout
                    // assertion (D-295), collapsing the whole onboarding body.
                    // Override to a content-width minimum for the in-Row usage.
                    style: FilledButton.styleFrom(
                      minimumSize: const Size(0, 48),
                    ),
                    onPressed: _onNext,
                    child: Text(
                      isLast ? l10n.onboardingGetStarted : l10n.onboardingNext,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProgressSegments extends StatelessWidget {
  const _ProgressSegments({required this.count, required this.activeIndex});

  final int count;
  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        for (int i = 0; i < count; i++)
          Container(
            margin: const EdgeInsets.symmetric(horizontal: 3),
            width: i == activeIndex ? 22 : 8,
            height: 3,
            decoration: BoxDecoration(
              color: i == activeIndex ? SimfTokens.accent : SimfTokens.line,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
      ],
    );
  }
}

/// One intro-video slide. The video player is an **interim placeholder frame**
/// (Page_002 FE-1..4): the real source is YouTube (`introd_001..003`) with a
/// bundled fallback, wired with SIMF-VID-001 — the clips are not delivered yet,
/// so this renders a play/mute frame standing in for the embedded player.
class _IntroVideoSlide extends StatelessWidget {
  const _IntroVideoSlide({
    required this.index,
    required this.title,
    required this.videoName,
  });

  final int index;
  final String title;
  final String videoName;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        SizedBox(
          height: 172,
          width: 300,
          child: Container(
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
              border: Border.all(color: SimfTokens.line2),
            ),
            clipBehavior: Clip.antiAlias,
            child: Stack(
              children: <Widget>[
                Center(
                  child: Container(
                    width: 46,
                    height: 46,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: SimfTokens.accent.withValues(alpha: 0.18),
                      border: Border.all(color: SimfTokens.accent),
                    ),
                    child: const Icon(
                      Icons.play_arrow_rounded,
                      color: SimfTokens.accent,
                      size: 20,
                    ),
                  ),
                ),
                PositionedDirectional(
                  top: SimfTokens.space2,
                  start: SimfTokens.space3,
                  child: const Text(
                    'YouTube',
                    style: TextStyle(
                      color: SimfTokens.txtTertiary,
                      fontSize: SimfTokens.textXs,
                      letterSpacing: 1,
                    ),
                  ),
                ),
                PositionedDirectional(
                  bottom: SimfTokens.space2,
                  end: SimfTokens.space3,
                  child: Text(
                    videoName,
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(
                      color: SimfTokens.txtSecondary,
                      fontSize: SimfTokens.textXs,
                    ),
                  ),
                ),
                PositionedDirectional(
                  bottom: SimfTokens.space2,
                  start: SimfTokens.space2,
                  child: Container(
                    width: 24,
                    height: 24,
                    decoration: const BoxDecoration(
                      shape: BoxShape.circle,
                      color: SimfTokens.line2,
                    ),
                    child: IconButton(
                      tooltip: l10n.onboardingMutedTooltip,
                      padding: EdgeInsets.zero,
                      iconSize: 14,
                      onPressed: () {},
                      icon: const Icon(
                        Icons.volume_off_outlined,
                        color: SimfTokens.txtSecondary,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          title,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: SimfTokens.surface,
            fontSize: SimfTokens.textLg,
            fontWeight: FontWeight.w700,
            height: 1.4,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          '${l10n.onboardingVideoLabel} ${index + 1} / 3',
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: SimfTokens.txtSecondary,
            fontSize: SimfTokens.textSm,
            height: 1.8,
          ),
        ),
      ],
    );
  }
}
