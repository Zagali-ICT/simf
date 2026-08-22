import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';

/// The gold intro banner (frame node 1388:7503): the welcome paragraph with a
/// guide glyph at the inline end, on the solid gold card.
class ForumGuideBanner extends StatelessWidget {
  const ForumGuideBanner({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:7503)
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Text(
              text,
              textAlign: TextAlign.start,
              style: SimfTokens.labelWhiteMediumTall,
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          const Icon(
            Icons.menu_book_outlined,
            color: SimfTokens.surface,
            size: SimfTokens.forumGuideCardsSizeSm,
          ),
        ],
      ),
    );
  }
}

/// One numbered step card (frame node 1388:7512): the gold index badge at the
/// inline start, the title over the muted description, and a decorative gold
/// caret at the inline end, on the navy-deep card chrome.
class ForumGuideStep extends StatelessWidget {
  const ForumGuideStep({
    required this.number,
    required this.title,
    required this.body,
    super.key,
  });

  final int number;
  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:7512)
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: SimfTokens.space8,
            height: SimfTokens.space8,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: SimfTokens.accent,
              borderRadius:
                  BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
            ),
            child: Text(
              '$number',
              style: SimfTokens.labelWhiteBoldMd,
            ),
          ),
          const SizedBox(width: SimfTokens.space2), // gap-8 (number → text)
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  textAlign: TextAlign.start,
                  style: SimfTokens.labelWhiteMedium,
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  body,
                  textAlign: TextAlign.start,
                  style: SimfTokens.bodyBeigeSm14,
                ),
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.gap18),
          const SimfForwardChevron(
            AppAssets.icCaretLeft,
            color: SimfTokens.accent,
            size: SimfTokens.forumGuideCardsSizeMd,
          ),
        ],
      ),
    );
  }
}
