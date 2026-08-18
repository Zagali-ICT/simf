/// Window-size breakpoints for responsive layout (CLAUDE.md §3).
///
/// Material-3 window-size classes, named so screens never hardcode
/// `if (width > 600)`. The app is portrait-locked, so these distinguish small
/// phones from large phones and tablets — not landscape.
library;

import 'package:flutter/widgets.dart';

/// The four width buckets:
/// compact < 600 ≤ medium < 905 ≤ expanded < 1240 ≤ large.
enum WindowSize {
  compact,
  medium,
  expanded,
  large;

  static WindowSize fromWidth(double width) {
    if (width < 600) {
      return WindowSize.compact;
    }
    if (width < 905) {
      return WindowSize.medium;
    }
    if (width < 1240) {
      return WindowSize.expanded;
    }
    return WindowSize.large;
  }

  static WindowSize of(BuildContext context) =>
      fromWidth(MediaQuery.sizeOf(context).width);

  /// A single phone column (compact); tablets get the wider treatments.
  bool get isCompact => this == WindowSize.compact;
}
