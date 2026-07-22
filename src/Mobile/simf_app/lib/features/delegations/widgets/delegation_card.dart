import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/delegation_models.dart';

/// One delegation card (Figma 1426:10838): country identity, the head-of-
/// delegation box (when set), and the date range + member count.
class DelegationCard extends StatelessWidget {
  const DelegationCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
    super.key,
  });

  final DelegationItem item;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      radius: SimfTokens.radius, // 8 (Figma 1426:10838)
      borderWidth: 0, // borderless
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            _identityRow(),
            if (item.hasHead) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              // _headBox(),
            ],
            const SizedBox(height: SimfTokens.space4),
            // _bottomRow(),
          ],
        ),
      ),
    );
  }

  Widget _identityRow() {
    final title = item.localizedCountry(isArabic);
    final subtitle = item.localizedCountrySubtitle(isArabic);
    // Only show the other-language name when it actually adds information — when
    // a country has just one name the title falls back to it, so the subtitle
    // would otherwise duplicate the title.
    final showSubtitle = subtitle.isNotEmpty && subtitle != title;
    return Row(
      children: <Widget>[
        _FlagBox(emoji: item.flagEmoji),
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
                const SizedBox(height: SimfTokens.space2), // 8 (Figma 1426:10840)
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

  Widget _headBox() {
    return Container(
      decoration: BoxDecoration(
        color: SimfTokens.goldFill6,
        border: Border.all(color: SimfTokens.goldBorder15),
        borderRadius: BorderRadius.circular(SimfTokens.radius10),
      ),
      padding: const EdgeInsets.all(SimfTokens.headBoxPad),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Row(
              children: <Widget>[
                _HeadAvatar(initial: item.headInitial(isArabic)),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        item.localizedHead(isArabic) ?? '',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: SimfTokens.labelGoldSemiboldSm,
                      ),
                      if (item.localizedHeadTitle(isArabic)?.trim().isNotEmpty ??
                          false) ...<Widget>[
                        const SizedBox(height: SimfTokens.gap2),
                        Text(
                          item.localizedHeadTitle(isArabic)!.trim(),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: SimfTokens.labelBeigeMedium10,
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          _HeadChip(label: l10n.delegationsHeadLabel),
        ],
      ),
    );
  }

  Widget _bottomRow() {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        // Flexible so a long member label / date range ellipsizes instead of
        // overflowing on a narrow device or with a large OS font scale.
        Flexible(child: _MemberChip(text: l10n.delegationsMembers(item.memberCount))),
        if (item.hasDateRange)
          Flexible(
            child: _DateGroup(
              text:
                  l10n.delegationsDateRange(item.arrivalDate, item.departureDate),
            ),
          )
        else
          const SizedBox.shrink(),
      ],
    );
  }
}

class _FlagBox extends StatelessWidget {
  const _FlagBox({required this.emoji});

  final String emoji;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.surfaceTint,
        border: Border.all(color: SimfTokens.line),
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
      ),
      child: Text(emoji, style: const TextStyle(fontSize: 28)),
    );
  }
}

class _HeadAvatar extends StatelessWidget {
  const _HeadAvatar({required this.initial});

  final String initial;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 38,
      height: 38,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initial,
        style: SimfTokens.labelWhiteBoldSm,
      ),
    );
  }
}

class _HeadChip extends StatelessWidget {
  const _HeadChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: SimfTokens.beigeFill10,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      child: Text(
        label,
        style: SimfTokens.labelGoldBold9,
      ),
    );
  }
}

class _MemberChip extends StatelessWidget {
  const _MemberChip({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: SimfTokens.beigeFill10,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      padding: const EdgeInsets.all(SimfTokens.space2),
      // Figma 1426:10862 — the groups glyph leads (inline-start = right in RTL),
      // the count text trails to its left.
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.groups_outlined,
            size: 12,
            color: SimfTokens.beigeBorder,
          ),
          const SizedBox(width: SimfTokens.gap6),
          Flexible(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: SimfTokens.labelBeigeSemibold11,
            ),
          ),
        ],
      ),
    );
  }
}

class _DateGroup extends StatelessWidget {
  const _DateGroup({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    // Figma 1426:10856 — the clock glyph leads (inline-start = right in RTL),
    // the date range trails to its left.
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        const Icon(Icons.schedule, size: 12, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space1),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelBeigeMedium10,
          ),
        ),
      ],
    );
  }
}
