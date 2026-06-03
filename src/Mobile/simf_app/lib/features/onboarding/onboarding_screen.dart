import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 002 — التهيئة · Onboarding (first-run only, Page_002 docs).
///
/// A three-slide intro carousel — the interim stand-in for the intro videos
/// (`introd_001..003`; the real YouTube/bundled player binds these logical
/// names later). It runs only on the first launch: the splash gates on the
/// `onboardingCompleted` flag, and finishing or skipping here sets that flag and
/// routes to sign-in. There is **no SIMF API** (Page_002_API.md).
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
    final slides = <(String, String)>[
      (l10n.onboardingTitle1, l10n.onboardingBody1),
      (l10n.onboardingTitle2, l10n.onboardingBody2),
      (l10n.onboardingTitle3, l10n.onboardingBody3),
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
                  itemBuilder: (context, i) {
                    final (title, body) = slides[i];
                    return _OnboardingSlide(title: title, body: body);
                  },
                ),
              ),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  TextButton(
                    onPressed: _onSkip,
                    child: Text(
                      l10n.onboardingSkip,
                      style: const TextStyle(color: SimfTokens.inkMuted),
                    ),
                  ),
                  FilledButton(
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
              color: i == activeIndex
                  ? SimfTokens.accent
                  : SimfTokens.surface.withValues(alpha: 0.24),
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
      ],
    );
  }
}

class _OnboardingSlide extends StatelessWidget {
  const _OnboardingSlide({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Container(
          width: 84,
          height: 84,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(
              color: SimfTokens.surface.withValues(alpha: 0.24),
            ),
          ),
          alignment: Alignment.center,
          child: const Icon(
            Icons.public_outlined,
            color: SimfTokens.accent,
            size: 38,
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
        Text(
          title,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: SimfTokens.surface,
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: SimfTokens.space3),
        Text(
          body,
          textAlign: TextAlign.center,
          style: TextStyle(
            color: SimfTokens.surface.withValues(alpha: 0.78),
            fontSize: SimfTokens.textMd,
            height: 1.7,
          ),
        ),
      ],
    );
  }
}
