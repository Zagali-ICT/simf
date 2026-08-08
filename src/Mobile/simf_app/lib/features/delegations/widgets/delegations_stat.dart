import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class DelegationsStat extends StatelessWidget {
  const DelegationsStat({
    required this.value,
    required this.label,
    required this.alignEnd,
  });

  final int value;
  final String label;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment:
          alignEnd ? CrossAxisAlignment.end : CrossAxisAlignment.start,
      children: <Widget>[
        Text('$value', style: SimfTokens.labelGoldBoldXl),
        Text(label, style: SimfTokens.labelBeigeSm),
      ],
    );
  }
}
