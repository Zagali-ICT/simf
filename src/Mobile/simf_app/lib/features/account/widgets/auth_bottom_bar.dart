import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';

/// A pinned bottom action on an auth screen, padded and capped to the same
/// width as the body above it.
class AuthBottomBar extends StatelessWidget {
  const AuthBottomBar({required this.maxWidth, required this.child, super.key});

  final double maxWidth;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(maxWidth: maxWidth, child: child),
    );
  }
}
