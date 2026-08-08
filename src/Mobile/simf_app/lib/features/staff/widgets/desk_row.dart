import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// One label / value line in the result card.
class DeskRow extends StatelessWidget {
  const DeskRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text('$label: ', style: SimfTokens.labelBeigeSm),
          Expanded(
            child: Text(value, style: SimfTokens.labelWhiteSemibold),
          ),
        ],
      ),
    );
  }
}
