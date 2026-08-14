import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class InfoRow extends StatelessWidget {
  const InfoRow({required this.icon, required this.value, required this.sublabel, required this.valueLtr, super.key,
  });

  final IconData icon;
  final String value;
  final String sublabel;
  final bool valueLtr;

  @override
  Widget build(BuildContext context) {
    return Container(
      // The frame walls each info row with a faint beige bottom divider
      // (Figma 1388:7723 — beige @25%).
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.beigeBorder.withValues(alpha: 0.25),
            width: SimfTokens.hairline,
          ),
        ),
      ),
      padding: const EdgeInsets.all(SimfTokens.space2), // p-8
      // Icon leads (right edge under RTL), value + sub-label follow to its
      // inline end — matches Figma 1388:7711.
      child: Row(
        children: <Widget>[
          Container(
            width: SimfTokens.space10,
            height: SimfTokens.space10,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: SimfTokens.accent,
              borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
            ),
            child: Icon(icon, color: SimfTokens.navy, size: SimfTokens.infoRowSize),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  value,
                  textAlign: TextAlign.start,
                  textDirection: valueLtr ? TextDirection.ltr : null,
                  style: SimfTokens.labelWhiteMedium,
                ),
                const SizedBox(height: SimfTokens.space2), // gap-8
                Text(
                  sublabel,
                  style: SimfTokens.labelBeigeSm,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
