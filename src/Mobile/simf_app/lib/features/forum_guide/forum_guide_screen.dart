import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/forum_guide/widgets/forum_guide_cards.dart';

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
          ForumGuideBanner(text: l10n.forumGuideIntro),
          const SizedBox(height: SimfTokens.space4), // gap-16
          for (final (index, (title, body)) in steps.indexed) ...<Widget>[
            if (index > 0) const SizedBox(height: SimfTokens.space4), // gap-16
            ForumGuideStep(number: index + 1, title: title, body: body),
          ],
        ],
      ),
    );
  }
}
