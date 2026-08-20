import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'package:simf_app/core/motion/motion_durations.dart';

/// How long a slide holds before the carousel moves on.
const Duration _slideInterval = Duration(seconds: 4);

/// The auto-advance half of a home carousel: the [PageController], the slide
/// index the dots read, and the timer that walks them forward.
///
/// `HighlightsCarousel` and `HomeHeroBanner` are different widgets over
/// different models, but their motion is the same widget behaviour and was
/// written out twice — including the shrink bug below, which the second copy
/// inherited from the first. Mixed into the [State], not wrapped around the
/// widget, because the hosts differ in where the [PageView] sits: the hero
/// stacks it under a scrim and an overlay, the carousel puts dots beneath it.
///
/// The host supplies [carouselItemCount] and wires [carouselController],
/// [carouselIndex] and [onCarouselPageChanged] into its own `PageView`.
mixin CarouselAutoAdvance<W extends StatefulWidget> on State<W> {
  /// How many slides the host is rendering right now.
  int get carouselItemCount;

  /// Drives the host's `PageView`.
  final PageController carouselController = PageController();

  /// The slide currently on screen — what the position dots point at.
  int carouselIndex = 0;

  Timer? _timer;

  /// The count the timer was last built against. A mixin cannot read the count
  /// off `oldWidget`, which is typed as the host's widget, so it keeps its own
  /// copy of what it last saw.
  int _knownItemCount = 0;

  @override
  void initState() {
    super.initState();
    _knownItemCount = carouselItemCount;
    _startAutoAdvance();
  }

  @override
  void didUpdateWidget(covariant W oldWidget) {
    super.didUpdateWidget(oldWidget);
    // Both lists load asynchronously and are re-delivered on every pull to
    // refresh while the State is reused, so initState's one-shot set-up is not
    // enough: the timer would keep cycling modulo the OLD count, and the
    // carousel would sit static after growing past a single slide.
    final count = carouselItemCount;
    if (count == _knownItemCount) {
      return;
    }
    _knownItemCount = count;
    _timer?.cancel();
    _timer = null;
    _clampIndexToLastPage(count);
    _startAutoAdvance();
  }

  /// Pass to the host `PageView`'s `onPageChanged`.
  void onCarouselPageChanged(int page) => setState(() => carouselIndex = page);

  void _clampIndexToLastPage(int count) {
    // A shrink leaves the scroll position past the new end, and PageController
    // settles it on the LAST page, not the first — `_PagePosition` re-runs the
    // ballistic back to the new maxScrollExtent, and reports no page change
    // while doing it. So the index must be clamped to that same end. Resetting
    // it to 0 instead is a clamp against the wrong end: the dots would point at
    // slide 1 while slide N is on screen, and the next tick would target the
    // page BEHIND the visible one and animate backwards.
    carouselIndex = count == 0 ? 0 : math.min(carouselIndex, count - 1);
    // Jump rather than let the position settle there on its own: jumpTo goes
    // idle first, which also kills an animateToPage still running from the last
    // tick. Cancelling the timer does not stop the animation it already
    // started, and that animation's target page may no longer exist.
    if (carouselController.hasClients) {
      carouselController.jumpToPage(carouselIndex);
    }
  }

  void _startAutoAdvance() {
    if (carouselItemCount <= 1) {
      return;
    }
    _timer = Timer.periodic(_slideInterval, (_) {
      if (!mounted || !carouselController.hasClients) {
        return;
      }
      unawaited(
        carouselController.animateToPage(
          (carouselIndex + 1) % carouselItemCount,
          duration: MotionDurations.carouselSlide,
          curve: Curves.easeInOut,
        ),
      );
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    carouselController.dispose();
    super.dispose();
  }
}
