import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import 'simf_bottom_nav.dart';

/// Shared KSA main-shell chrome for the Wave-2 in-app pages (frames
/// 512:1492 / 203:1236 / 512:1780 / 215:767 / 221:769 / 215:562): the navy
/// page scaffold with the standard header, plus the tile / list-row /
/// section-header building blocks every page composes. One widget per
/// repeated frame element — pages never copy-paste shell markup.

/// The navy page scaffold: background, optional decorative sweep, a
/// forced-LTR header (circled back chevron at the left, centred title — the
/// D-363 chrome pattern), the page [body], and the shared bottom nav.
///
/// Pages with a non-standard header (e.g. the signed-in home's greeting row)
/// pass [header] instead of [title]; [tab] null hides the bottom nav (e.g.
/// in-flow pages), and [onBack] null hides the back button.
class KsaPage extends StatelessWidget {
  const KsaPage({
    required this.body,
    this.title,
    this.header,
    this.onBack,
    this.tab,
    this.showBottomNav = true,
    this.showSweep = false,
    this.background = SimfTokens.navySurface,
    super.key,
  });

  /// The page content, laid below the header (not scrollable by itself —
  /// pages own their scrolling).
  final Widget body;

  /// Centred header title (ignored when [header] is set).
  final String? title;

  /// Replaces the default back+title header row entirely.
  final Widget? header;

  /// Back-chevron action; null hides the back button.
  final VoidCallback? onBack;

  /// The active bottom-nav tab; null shows the bar with no active tab.
  final SimfTab? tab;

  /// False removes the bottom nav entirely.
  final bool showBottomNav;

  /// Renders the decorative rotated sweep (the entry frames' 28.28° block).
  final bool showSweep;

  final Color background;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: background,
      bottomNavigationBar:
          showBottomNav ? SimfBottomNav(current: tab) : null,
      body: Stack(
        children: <Widget>[
          if (showSweep) const KsaSweep(),
          SafeArea(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                header ?? _defaultHeader(),
                Expanded(child: body),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _defaultHeader() {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          SizedBox(
            width: 40,
            height: 40,
            child: onBack == null ? null : KsaBackButton(onBack: onBack!),
          ),
          Expanded(
            child: Text(
              title ?? '',
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: SimfTokens.textXl,
                fontWeight: FontWeight.w500,
                color: Colors.white,
              ),
            ),
          ),
          // Balances the back button so the title stays centred.
          const SizedBox(width: 40, height: 40),
        ],
      ),
    );
  }
}

/// The circled back chevron (dark circle, white LTR chevron — frames place
/// it at the physical left in both languages, the D-363 pattern).
class KsaBackButton extends StatelessWidget {
  const KsaBackButton({required this.onBack, super.key});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      onPressed: onBack,
      style: IconButton.styleFrom(
        backgroundColor: SimfTokens.navyDeep,
        shape: const CircleBorder(),
      ),
      icon: const Icon(
        Icons.arrow_back_ios_new,
        color: Colors.white,
        size: 18,
        textDirection: TextDirection.ltr,
      ),
    );
  }
}

/// The decorative rotated sweep block from the KSA entry frames (28.28°,
/// white-4% fill) — shared so pages stop copy-pasting the transform.
class KsaSweep extends StatelessWidget {
  const KsaSweep({super.key});

  @override
  Widget build(BuildContext context) {
    return Positioned(
      top: -156,
      left: 60,
      child: Transform.rotate(
        angle: 0.4936,
        child: Container(
          width: 313,
          height: 323,
          decoration: BoxDecoration(
            color: SimfTokens.surfaceTint,
            borderRadius: BorderRadius.circular(40),
          ),
        ),
      ),
    );
  }
}

/// A section title row (e.g. "معلومات مفتوحة للجميع", "عن الملتقى · المحاور")
/// with an optional trailing "more" action.
class KsaSectionHeader extends StatelessWidget {
  const KsaSectionHeader({
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
            style: const TextStyle(
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w500,
              color: Colors.white,
            ),
          ),
        ),
        if (onMore != null && moreLabel != null)
          InkWell(
            onTap: onMore,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space2,
                vertical: SimfTokens.space1,
              ),
              child: Text(
                moreLabel!,
                style: const TextStyle(
                  fontSize: SimfTokens.textSm,
                  color: SimfTokens.txtSecondary,
                ),
              ),
            ),
          ),
      ],
    );
  }
}

/// One navy feature tile (frame tiles "المتحدثون" / "الجلسات" / …): a 72-high
/// `navyDeep` card with a gold icon over a small white label. [enabled]
/// false renders the locked variant (the "بطاقتي" card / the disabled theme
/// tile) on the disabled palette with no tap.
class KsaNavTile extends StatelessWidget {
  const KsaNavTile({
    required this.label,
    required this.icon,
    this.onTap,
    this.enabled = true,
    this.trailing,
    super.key,
  });

  final String label;
  final IconData icon;
  final VoidCallback? onTap;
  final bool enabled;

  /// Optional second line under the label (e.g. a stat value uses its own
  /// widget instead — see [KsaStatTile]).
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final foreground =
        enabled ? SimfTokens.accent : SimfTokens.navyDisabledText;
    final labelColor = enabled ? Colors.white : SimfTokens.navyDisabledText;
    return Material(
      color: enabled ? SimfTokens.navyDeep : SimfTokens.navyDisabled,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: BorderSide(
          color: enabled
              ? SimfTokens.beigeBorder
              : SimfTokens.navyDisabledBorder,
          width: enabled ? 0.2 : 1,
        ),
      ),
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 72),
          child: Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Icon(icon, size: 24, color: foreground),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  label,
                  textAlign: TextAlign.center,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w600,
                    color: labelColor,
                  ),
                ),
                if (trailing != null) trailing!,
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A stat tile (frames 512:1780 / 213:963): a big gold number over its label,
/// on the same card chrome as [KsaNavTile].
class KsaStatTile extends StatelessWidget {
  const KsaStatTile({required this.value, required this.label, super.key});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(color: SimfTokens.beigeBorder, width: 0.2),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: 72),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Text(
                '$value',
                style: const TextStyle(
                  fontSize: SimfTokens.textXl,
                  fontWeight: FontWeight.w700,
                  color: SimfTokens.accent,
                ),
              ),
              const SizedBox(height: SimfTokens.space1),
              Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                  color: Colors.white,
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
class KsaListRow extends StatelessWidget {
  const KsaListRow({
    required this.title,
    required this.onTap,
    this.subtitle,
    this.badge,
    super.key,
  });

  final String title;
  final String? subtitle;

  /// The 72×64 gold box content (an icon or short text); null omits the box.
  final Widget? badge;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(color: SimfTokens.goldSoft, width: 0.5),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Row(
            children: <Widget>[
              if (badge != null) ...<Widget>[
                Container(
                  width: 72,
                  height: 64,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: SimfTokens.accent,
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
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
                      style: const TextStyle(
                        fontSize: SimfTokens.textLg,
                        fontWeight: FontWeight.w600,
                        color: Colors.white,
                      ),
                    ),
                    if (subtitle != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space2),
                      Text(
                        subtitle!,
                        style: const TextStyle(
                          fontSize: SimfTokens.textMd,
                          color: SimfTokens.beigeBorder,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              const Icon(
                Icons.arrow_left,
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
