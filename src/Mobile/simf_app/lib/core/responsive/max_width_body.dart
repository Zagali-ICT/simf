/// A page-body wrapper that caps content width and centres it, so content does
/// not stretch edge-to-edge on wide screens / tablets (CLAUDE.md §3).
///
/// Wrap a screen's scroll/column body in this. Defaults to 840 (reading width);
/// pass `maxWidth: 560` for forms. Spacing inside the cap is the caller's
/// responsibility — pass `padding` (token-based) rather than hardcoding widths.
library;

import 'package:flutter/material.dart';

class MaxWidthBody extends StatelessWidget {
  const MaxWidthBody({
    required this.child,
    this.maxWidth = 840,
    this.padding = EdgeInsets.zero,
    super.key,
  });

  final Widget child;
  final double maxWidth;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: maxWidth),
        child: Padding(
          padding: padding,
          child: child,
        ),
      ),
    );
  }
}
