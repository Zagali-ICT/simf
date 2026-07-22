import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// A section heading (frame 889:2717/889:2720/889:2770): white, 16px Medium,
/// right-aligned for RTL.
class SessionSectionHeading extends StatelessWidget {
  const SessionSectionHeading(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: SimfTokens.labelWhiteMediumLg,
    );
  }
}

/// The description card (frame 889:2719): a navy box with the description in
/// white, 14px, comfortable line height.
class SessionDescriptionCard extends StatelessWidget {
  const SessionDescriptionCard({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Text(
        text,
        style: SimfTokens.bodyWhite,
      ),
    );
  }
}
