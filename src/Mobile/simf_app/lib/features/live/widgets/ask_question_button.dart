import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The ask-a-question entry (frame's L-3 Q&A affordance) → Page 026
/// (`/live/question?sessionId=`). A full-width gold action button.
class AskQuestionButton extends StatelessWidget {
  const AskQuestionButton(
      {required this.label, required this.onTap, super.key,});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.controlHeight,
      child: FilledButton.icon(
        onPressed: onTap,
        icon:
            const Icon(Icons.help_outline, size: SimfTokens.liveContentSizeSm),
        // The size/weight ride the label Text (not styleFrom.textStyle) so the
        // Arabic label keeps the theme's brand font — an inline
        // `styleFrom.textStyle` drops fontFamily and tofus the Arabic (the
        // recurring button-font bug, D-546/D-549).
        label: Text(
          label,
          style: SimfTokens.titleBold,
        ),
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.surface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
      ),
    );
  }
}
