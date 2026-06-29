import 'package:flutter/material.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';

/// Page 200 — دليل الملتقى · Forum guide (`/forum-guide`, public). Pixel-parity
/// to KSA Figma frame **1388:7493**: the navy [SimfPageShell] shell, a gold intro
/// banner, then five numbered step cards (gold index badge + title + muted
/// description) on the navy-deep card chrome.
///
/// Static in-app copy — no backend (the steps live in [AppL10n]). The caret on
/// each card is decorative, matching the design (the steps do not navigate).
class ForumGuideScreen extends StatelessWidget {
  const ForumGuideScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final steps = <(String, String)>[
      (l10n.forumGuideStep1Title, l10n.forumGuideStep1Body),
      (l10n.forumGuideStep2Title, l10n.forumGuideStep2Body),
      (l10n.forumGuideStep3Title, l10n.forumGuideStep3Body),
      (l10n.forumGuideStep4Title, l10n.forumGuideStep4Body),
      (l10n.forumGuideStep5Title, l10n.forumGuideStep5Body),
    ];
    return SimfPageShell(
      title: l10n.forumGuideTitle,
      onBack: () => backOrHome(context),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          _IntroBanner(text: l10n.forumGuideIntro),
          const SizedBox(height: SimfTokens.space4), // gap-16
          for (final (index, (title, body)) in steps.indexed) ...<Widget>[
            if (index > 0) const SizedBox(height: SimfTokens.space4), // gap-16
            _GuideStep(number: index + 1, title: title, body: body),
          ],
        ],
      ),
    );
  }
}

/// The gold intro banner (frame node 1388:7503): the welcome paragraph with a
/// guide glyph at the inline end, on the solid gold card.
class _IntroBanner extends StatelessWidget {
  const _IntroBanner({required this.text});

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
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w500,
                height: 1.4,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          const Icon(
            Icons.menu_book_outlined,
            color: Colors.white,
            size: 14,
          ),
        ],
      ),
    );
  }
}

/// One numbered step card (frame node 1388:7512): the gold index badge at the
/// inline start, the title over the muted description, and a decorative gold
/// caret at the inline end, on the navy-deep card chrome.
class _GuideStep extends StatelessWidget {
  const _GuideStep({
    required this.number,
    required this.title,
    required this.body,
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
            width: 32,
            height: 32,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: SimfTokens.accent,
              borderRadius:
                  BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
            ),
            child: Text(
              '$number',
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd, // 14
                fontWeight: FontWeight.w700,
              ),
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
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd, // 14
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  body,
                  textAlign: TextAlign.start,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm, // 12
                    height: 1.4,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 18), // gap-18 (content → caret, Figma 1426:11374)
          const SimfSvgIcon(
            'assets/icons/ic_caret_left.svg',
            color: SimfTokens.accent,
            size: 20,
          ),
        ],
      ),
    );
  }
}
