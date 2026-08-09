import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/moderation/widgets/role_pill.dart';

/// The moderator desk navy header (Figma 1461:12565): back button (left),
/// centred RTL title, and the gold role pill (right). Forced LTR so the back
/// button sits on the left as drawn.
class ModeratorDeskHeader extends StatelessWidget {
  const ModeratorDeskHeader({
    required this.title,
    required this.badgeLabel,
    required this.onBack,
    super.key,
  });

  final String title;
  final String badgeLabel;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: SimfTokens.navyHeader,
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space2,
        SimfTokens.space1,
        SimfTokens.space4,
        SimfTokens.space4,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          // Circular navy back button with a left chevron (Figma).
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Material(
              color: SimfTokens.navyDeep,
              shape: const CircleBorder(),
              child: InkWell(
                customBorder: const CircleBorder(),
                onTap: onBack,
                child: const Padding(
                  padding: EdgeInsets.all(SimfTokens.gap6),
                  child: Icon(
                    Icons.chevron_left,
                    color: SimfTokens.surface,
                    size: SimfTokens.moderatorHeaderSize,
                  ),
                ),
              ),
            ),
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              textDirection: TextDirection.rtl,
              style: SimfTokens.labelWhiteBoldHero,
            ),
          ),
          RolePill(label: badgeLabel),
        ],
      ),
    );
  }
}

