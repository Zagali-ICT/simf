import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The frame 943:3750 footnote — a single centred gold bullet, the bold gold
/// "ملاحظة" word, then the muted-beige "reviewed before air" body.
class ReviewNote extends StatelessWidget {
  const ReviewNote({required this.label, required this.body, super.key});

  final String label;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          const TextSpan(
            text: '• ',
            style: SimfTokens.textAccent,
          ),
          TextSpan(
            text: '$label ',
            style: SimfTokens.labelGoldSemiboldLg,
          ),
          TextSpan(
            text: body,
            style: SimfTokens.bodyBeigeMd,
          ),
        ],
      ),
      textAlign: TextAlign.center,
    );
  }
}
