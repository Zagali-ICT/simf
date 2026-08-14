import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart' show SimfEmptyState;
import 'package:simf_app/app/widgets/simf_states.dart' show SimfEmptyState;
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// The card / section-header / link-row / list-row surfaces every page
/// composes. Split out of `simf_page_shell.dart`, which re-exports them so
/// every existing import keeps working.

/// The W2 base card chrome — the `navyDeep` fill with the beige hairline on
/// the small radius — shared by every tile/row in the batch so the border /
/// fill / radius have one owner. [onTap] null renders a plain surface.
class SimfCard extends StatelessWidget {
  const SimfCard({
    required this.child,
    this.onTap,
    this.color = SimfTokens.navyDeep,
    this.borderColor = SimfTokens.beigeBorder,
    this.borderWidth = SimfTokens.hairline,
    this.radius = SimfTokens.radiusSmall,
    super.key,
  });

  final Widget child;
  final VoidCallback? onTap;
  final Color color;
  final Color borderColor;
  final double borderWidth;

  /// Corner radius — defaults to the W2 small radius; the exhibitor/sponsor
  /// identity + about cards override to 8 and the link rows to 14 (Figma
  /// 1439:11881).
  final double radius;

  @override
  Widget build(BuildContext context) {
    final br = BorderRadius.all(Radius.circular(radius));
    return Material(
      color: color,
      shape: RoundedRectangleBorder(
        borderRadius: br,
        // width 0 in Flutter still paints a 1px hairline; a borderless card
        // (the exhibitor/sponsor identity + about cards) needs BorderSide.none.
        side: borderWidth <= 0
            ? BorderSide.none
            : BorderSide(color: borderColor, width: borderWidth),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: br,
        child: child,
      ),
    );
  }
}

/// A section title row (e.g. "معلومات مفتوحة للجميع", "عن الملتقى · المحاور")
/// with an optional trailing "more" action.
class SimfSectionHeader extends StatelessWidget {
  const SimfSectionHeader({
    required this.title,
    this.moreLabel,
    this.onMore,
    super.key,
  });

  final String title;

  /// The trailing action text (e.g. l10n.moreTitle); rendered only with [onMore].
  final String? moreLabel;
  final VoidCallback? onMore;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            title,
            style: SimfTokens.labelWhiteMediumLg,
          ),
        ),
        if (onMore != null && moreLabel != null)
          InkWell(
            onTap: onMore,
            borderRadius:
                const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
            child: Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space2,
                vertical: SimfTokens.space1,
              ),
              child: Text(
                moreLabel!,
                // Frame 758:1134 — the "more" link is white, Medium.
                style: SimfTokens.labelWhiteMediumSm,
              ),
            ),
          ),
      ],
    );
  }
}

/// A short muted explanatory note under a page title — the shared "what is this
/// screen for" line (BUG-025: My Visitors vs My Contacts). An info glyph plus one
/// wrapping paragraph, RTL-safe via the surrounding directionality. Distinct from
/// [SimfSectionHeader] (a bold section title) and [SimfEmptyState] (a centred
/// empty surface): this sits above real content and is always shown.
class SimfPageNote extends StatelessWidget {
  const SimfPageNote({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        // Sized like the shell's other inline glyphs (cf. SimfEmptyState).
        const Icon(Icons.info_outline, size: SimfTokens.simfCardsSizeSm, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space2),
        Expanded(child: Text(text, style: SimfTokens.bodyBeigeSm)),
      ],
    );
  }
}

/// A bordered single-line link row — the signed-in home's section bars
/// (frames 758:1207 / 1049:12844 / 758:1211 "عن الملتقى" / "الرعاة" /
/// "الأخبار والتغطية"): a transparent 48-high box with the beige hairline, the
/// title at the inline end (physical right under RTL) and a gold caret at the
/// inline start. Tappable. Distinct from [SimfSectionHeader] (a plain text label)
/// and [SimfListRow] (which carries a gold badge box + subtitle).
class SimfLinkRow extends StatelessWidget {
  const SimfLinkRow({required this.title, required this.onTap, super.key});

  final String title;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final flip = !AppL10n.of(context).isArabic;
    return SimfCard(
      onTap: onTap,
      color: SimfTokens.transparent,
      child: SizedBox(
        height: SimfTokens.simfCardsHeightSm,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  title,
                  textAlign: TextAlign.start,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelWhiteMediumLg,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              // Home section-bar caret. Figma 758:1208 / 1049:12845 / 758:1212
              // (عن الملتقى / الرعاه / الأخبار) fill this caret WHITE — only the
              // روح السعودية row (758:1275, [SimfListRow]) keeps it gold. The
              // bundled SVG does not auto-mirror under RTL, so it stays pointing
              // left as the design shows. Flip horizontally in English so the
              // caret points right → (forward in LTR reading direction).
              Transform.flip(
                flipX: flip,
                child: const SimfSvgIcon(
                  AppAssets.icCaretLeft,
                  color: SimfTokens.surface,
                  size: SimfTokens.simfCardsSizeMd,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// One navy list row (frames' FAQ / روح السعودية / المزيد rows): a gold badge
/// box at the inline start, a bold title + muted subtitle, and a gold
/// forward arrow at the inline end.
class SimfListRow extends StatelessWidget {
  const SimfListRow({
    required this.title,
    required this.onTap,
    this.subtitle,
    this.badge,
    this.badgeOutlined = false,
    super.key,
  });

  final String title;
  final String? subtitle;

  /// The 72×64 badge box content (an icon or short text); null omits the box.
  final Widget? badge;

  /// When true the badge box is the **outlined** variant — a gold hairline over
  /// the card fill instead of a solid gold fill (the guest-home FAQ / روح
  /// السعودية rows, frame 758:2910). The caller colours [badge] to match
  /// (gold content on the outlined box; white on the filled box).
  final bool badgeOutlined;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final flip = !AppL10n.of(context).isArabic;
    return SimfCard(
      onTap: onTap,
      borderColor: SimfTokens.goldSoft,
      borderWidth: SimfTokens.hairlineBold,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            if (badge != null) ...<Widget>[
              Container(
                width: SimfTokens.simfCardsWidth,
                height: SimfTokens.simfCardsHeightMd,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: badgeOutlined ? null : SimfTokens.accent,
                  border: badgeOutlined
                      ? Border.all(color: SimfTokens.accent)
                      : null,
                  borderRadius: const BorderRadius.all(
                    Radius.circular(SimfTokens.radius),
                  ),
                ),
                child: badge,
              ),
              const SizedBox(width: SimfTokens.space3),
            ],
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: SimfTokens.labelWhiteSemiboldLg,
                  ),
                  if (subtitle != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      subtitle!,
                      style: SimfTokens.bodyBeigeMd,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Frame 758:1274 — a gold left-pointing caret. Material's
            // Icons.arrow_left auto-mirrors to the right under RTL; the bundled
            // SVG does not, so it stays pointing left as the design shows.
            // Flip horizontally in English so the caret points right →
            // (forward in LTR reading direction).
            Transform.flip(
              flipX: flip,
              child: const SimfSvgIcon(
                AppAssets.icCaretLeft,
                color: SimfTokens.accent,
                size: SimfTokens.simfCardsSizeMd,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
