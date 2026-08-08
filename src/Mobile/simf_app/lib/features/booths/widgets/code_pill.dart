import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The accent booth-number pill (frame node 922:2796): a 109-wide navy box with
/// a gold hairline, the code in gold, always LTR.
class CodePill extends StatelessWidget {
  const CodePill({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.codePillWidth,
      height: SimfTokens.controlHeight,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        code,
        textDirection: TextDirection.ltr,
        style: SimfTokens.labelGoldSemibold,
      ),
    );
  }
}
