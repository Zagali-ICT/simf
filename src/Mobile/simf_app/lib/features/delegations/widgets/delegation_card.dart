import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/widgets/flag_box.dart';

/// One delegation card (Figma 1426:10838): the country identity row (flag +
/// bilingual name). The head-of-delegation box and the member-count /
/// date-range bottom row are intentionally hidden in the current layout (owner
/// 2026-07-24).
class DelegationCard extends StatelessWidget {
  const DelegationCard({
    required this.item,
    required this.isArabic,
    this.onTap,
    super.key,
  });

  final DelegationItem item;
  final bool isArabic;

  /// Bi-Meeting rework — when set (the signed-in user holds
  /// AllowsDelegationMeeting) tapping the card opens the delegation
  /// meeting-request sheet for this country. Null → the card is a plain,
  /// non-interactive info card (guests / unentitled).
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final card = SimfCard(
      radius: SimfTokens.radius, // 8 (Figma 1426:10838)
      borderWidth: 0, // borderless
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: _identityRow(),
      ),
    );
    if (onTap == null) {
      return card;
    }
    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: card,
    );
  }

  Widget _identityRow() {
    final title = item.localizedCountry(isArabic: isArabic);
    final subtitle = item.localizedCountrySubtitle(isArabic: isArabic);
    // Only show the other-language name when it actually adds information —
    // when a country has just one name the title falls back to it, so the
    // subtitle would otherwise duplicate the title.
    final showSubtitle = subtitle.isNotEmpty && subtitle != title;
    return Row(
      children: <Widget>[
        FlagBox(emoji: item.flagEmoji),
        const SizedBox(width: SimfTokens.space3),
        Expanded(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: SimfTokens.labelWhiteBold15,
              ),
              if (showSubtitle) ...<Widget>[
                const SizedBox(
                    height: SimfTokens.space2,), // 8 (Figma 1426:10840)
                Text(
                  subtitle,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelBeigeMediumSm,
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}
