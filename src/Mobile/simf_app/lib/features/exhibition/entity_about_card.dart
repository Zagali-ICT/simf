import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';

/// The "نبذة عن…" about card (Figma 1439:11931): borderless navyDeep, radius-8;
/// white Medium-16 right-aligned header, a beige hairline divider, then the
/// beige Regular-14 paragraph at line-height 1.5, right-aligned.
class EntityAboutCard extends StatelessWidget {
  const EntityAboutCard({required this.header, required this.body, super.key});

  final String header;
  final String body;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      radius: SimfTokens.radius, // 8
      borderWidth: 0,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              header,
              textAlign: TextAlign.start,
              style: SimfTokens.labelWhiteMediumLg, // 16
            ),
            const SizedBox(height: SimfTokens.space3), // 12
            Container(
              height: SimfTokens.hairlineBold,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(height: SimfTokens.space2), // 8
            Text(
              body,
              textAlign: TextAlign.start,
              style: SimfTokens.bodyBeige, // beige 14, height 1.5
            ),
          ],
        ),
      ),
    );
  }
}
