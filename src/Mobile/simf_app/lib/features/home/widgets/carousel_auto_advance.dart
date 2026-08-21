import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'package:simf_app/core/motion/motion_durations.dart';

const Duration _slideInterval = Duration(seconds: 4);

/// The auto-advance half of a home carousel: the [PageController], the slide
/// index the dots read, and the timer that walks them forward.
///
/// Mixed into the [State] rather than wrapped around the widget, because the
/// hosts differ in where the [PageView] sits. The host supplies
/// [carouselItemCount] and wires [carouselController], [carouselIndex] and
/// [onCarouselPageChanged] into its own `PageView`.
mixin CarouselAutoAdvance<W extends StatefulWidget> on State<W> {
  int get carouselItemCount;

  /// Drives the host's `PageView`.
  final PageController carouselController = PageController();

  /// The slide currently on screen — what the position dots point at.
  int carouselIndex = 0;

  Timer? _timer;

  /// The count the timer was last built against — a mixin cannot read it off
  /// `oldWidget`, which is typed as the host's widget.
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
    // The list arrives asynchronously and is re-delivered on every refresh
    // while the State is reused, so a timer built once in initState would keep
    // cycling modulo the OLD count.
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
    // Clamp to the LAST page, not to 0: a shrink leaves the scroll position
    // past the new end and PageController settles it on the final page.
    carouselIndex = count == 0 ? 0 : math.min(carouselIndex, count - 1);
    // jumpTo goes idle first, which also kills an animateToPage still running
    // from the last tick towards a page that may no longer exist.
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
