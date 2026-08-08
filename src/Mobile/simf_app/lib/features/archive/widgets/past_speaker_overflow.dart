import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The past-speakers overflow tile (frame node 927:3343): a 72×72 beige-bordered
/// rounded-rect (r8) with a big gold "+N" over the white "آخرون" label.
class PastSpeakerOverflow extends StatelessWidget {
  const PastSpeakerOverflow({required this.count, required this.label});

  final int count;
  final String label;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 72,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 72,
            height: 72,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              border: Border.all(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
            ),
            child: Text(
              '+$count',
              textDirection: TextDirection.ltr,
              style: SimfTokens.labelGoldBoldTitle,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            maxLines: 1,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelWhiteSemiboldSm,
          ),
        ],
      ),
    );
  }
}
