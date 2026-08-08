import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class StatTile extends StatelessWidget {
  const StatTile({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Column(
        children: <Widget>[
          Text(
            '$value',
            textDirection: TextDirection.ltr,
            style: SimfTokens.labelGoldSemiboldTitle,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelBeigeSm,
          ),
        ],
      ),
    );
  }
}
