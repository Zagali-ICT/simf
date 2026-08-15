import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The frame 942:3746 gold full-width submit: white SemiBold label on the
/// 4px-radius accent fill. The size/weight ride the label [Text] (not
/// `styleFrom.textStyle`) so the Arabic label keeps the theme's brand font — an
/// inline `styleFrom.textStyle` drops fontFamily and tofus the Arabic
/// (D-546/D-549; the frozen golden had locked that tofu).
class SendQuestionSubmitButton extends StatelessWidget {
  const SendQuestionSubmitButton({
    required this.label,
    required this.onPressed,
    super.key,
  });

  final String label;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: SimfTokens.tapTarget,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.surface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
        child: Text(
          label,
          style: SimfTokens.labelSemiboldSm,
        ),
      ),
    );
  }
}
