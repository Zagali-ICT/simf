import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The منطقتي header card (frame 1129:16898): the user's avatar, the "منطقتي"
/// title over the "{name} · {tier}" sub-line, and the gold caret.
class MoreProfileCard extends StatelessWidget {
  const MoreProfileCard({
    required this.name,
    required this.tier,
    required this.onTap,
    super.key,
  });

  final String name;
  final String? tier;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final subtitle = <String>[
      if (name.trim().isNotEmpty) name.trim(),
      if (tier != null && tier!.trim().isNotEmpty) tier!.trim(),
    ].join(' · ');
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.moreMyAreaCardTitle,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: SimfTokens.textMd,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    if (subtitle.isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        subtitle,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              SimfAvatar(name: name, currentUser: true, size: 42),
              const SizedBox(width: SimfTokens.space2),
              const SimfSvgIcon(
                'assets/icons/ic_caret_left.svg',
                color: SimfTokens.accent,
                size: 24,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
