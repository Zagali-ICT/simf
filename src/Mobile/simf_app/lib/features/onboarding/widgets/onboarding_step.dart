import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One carousel step (Figma 148:22 / 159:943 / 159:1053): the step's own title
/// over its body copy, centred.
class OnboardingStep extends StatelessWidget {
  const OnboardingStep({required this.title, required this.body, super.key});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            title,
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteSemibold24Tall,
          ),
          const SizedBox(height: SimfTokens.space3),
          Flexible(
            child: Text(
              body,
              textAlign: TextAlign.center,
              style: SimfTokens.bodyBeigeTitleTall,
            ),
          ),
        ],
      ),
    );
  }
}
