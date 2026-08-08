import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The shared navy-deep card chrome for the About sections.
class AboutCard extends StatelessWidget {
  const AboutCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}
