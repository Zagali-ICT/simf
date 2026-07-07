import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The active-tab content card (frame 1072:14673): a navy (#01132D) box with the
/// gold-bar heading (bar on the right) and the bullets / note below.
class SummaryTabContentCard extends StatelessWidget {
  const SummaryTabContentCard({
    required this.heading,
    required this.child,
    super.key,
  });

  final String heading;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        // start = leading edge (right in RTL / left in LTR): the frame places
        // the heading + bullets on the right in Arabic; .end rendered LEFT.
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SummarySectionHeading(heading),
          const SizedBox(height: SimfTokens.space4),
          child,
        ],
      ),
    );
  }
}

/// A section heading (frame 1072:14660): the white Bold label with a gold pill
/// bar (4×20) to its inline-end (right under RTL).
class SummarySectionHeading extends StatelessWidget {
  const SummarySectionHeading(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 4,
          height: 20,
          decoration: BoxDecoration(
            color: SimfTokens.accent,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Text(
          text,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ],
    );
  }
}

/// One bullet row (frame 1072:14666): a 6px gold dot on the inline-start (right
/// under RTL) and the beige point text.
class SummaryBullet extends StatelessWidget {
  const SummaryBullet({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const Padding(
          padding: EdgeInsets.only(top: 7),
          child: SizedBox(
            width: 6,
            height: 6,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: SimfTokens.accent,
                shape: BoxShape.circle,
              ),
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            text,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w400,
              height: 1.5,
            ),
          ),
        ),
      ],
    );
  }
}
