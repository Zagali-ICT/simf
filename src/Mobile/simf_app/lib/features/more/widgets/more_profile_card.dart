import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

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
                      style: SimfTokens.labelWhiteBoldMd,
                    ),
                    if (subtitle.isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        subtitle,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: SimfTokens.labelBeigeSm,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              SimfAvatar(name: name, currentUser: true),
              const SizedBox(width: SimfTokens.space2),
              const SimfForwardChevron(
                AppAssets.icCaretLeft,
                color: SimfTokens.accent,
                size: SimfTokens.moreProfileCardSizeSm,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
