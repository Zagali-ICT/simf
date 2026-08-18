import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/content/widgets/terms_bullet_card.dart';

/// The loaded terms: the معلومات هامة heading over one gold-hairline bullet
/// card per [bullets] line, with the موافق button pinned below the scroll area.
class TermsBody extends StatelessWidget {
  const TermsBody({
    required this.bullets,
    required this.headingLabel,
    required this.acceptLabel,
    required this.onAccept,
    super.key,
  });

  final List<String> bullets;
  final String headingLabel;
  final String acceptLabel;
  final VoidCallback onAccept;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            // Full-width content (owner 2026-06-20): the cards stretch to the
            // page width instead of the old 400-wide centred column.
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                const SizedBox(height: SimfTokens.space4),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: Text(
                    headingLabel,
                    style: SimfTokens.labelWhiteBoldLg,
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                for (final item in bullets) ...<Widget>[
                  TermsBulletCard(text: item),
                  const SizedBox(height: SimfTokens.space4),
                ],
                const SizedBox(height: SimfTokens.space2),
              ],
            ),
          ),
        ),
        // The frame shows موافق unconditionally (505:1684); standalone it
        // simply leaves the page, in consent mode it returns true (D-375).
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            0,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          child: SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: onAccept,
              child: Text(
                acceptLabel,
                style: SimfTokens.titleBold,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
