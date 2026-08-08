import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The desk's navy surface — one shared card so the scanner, the result and the
/// error state never drift apart.
class DeskCard extends StatelessWidget {
  const DeskCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}
