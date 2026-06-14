import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../localization/app_l10n.dart';
import '../route_names.dart';
import '../theme/tokens.dart';
import 'more_drawer.dart';
import 'simf_bottom_nav.dart';

/// Shared KSA main-shell chrome for the Wave-2 in-app pages (frames
/// 512:1492 / 203:1236 / 512:1780 / 215:767 / 221:769 / 215:562): the navy
/// page scaffold with the standard header, plus the card / tile / list-row /
/// state-surface building blocks every page composes. One widget per
/// repeated frame element — pages never copy-paste shell markup.

/// The standard back action: pop when possible, else land on home.
void ksaBackOrHome(BuildContext context) {
  if (context.canPop()) {
    context.pop();
  } else {
    context.goNamed(RouteNames.home);
  }
}

/// The navy page scaffold: background, optional decorative sweep, a
/// forced-LTR header (circled back chevron at the left, centred title — the
/// D-363 chrome pattern), the page [body], and the shared bottom nav.
///
/// Pages with a non-standard header (e.g. the signed-in home's greeting row)
/// pass [header] instead of [title]; with no [header], [title] and [onBack]
/// the header row collapses entirely (full-bleed pages like the map).
/// [onBack] null hides the back button.
class KsaPage extends StatelessWidget {
  const KsaPage({
    required this.body,
    this.title,
    this.header,
    this.onBack,
    this.tab,
    this.showSweep = false,
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

  /// Renders the decorative rotated sweep (the entry frames' 28.28° block).
  final bool showSweep;

  @override
  Widget build(BuildContext context) {
    final headerRow = header ??
        (title == null && onBack == null ? null : _defaultHeader(context));
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      // The shared shell's side menu — the same المزيد drawer on every page
      // that uses this scaffold (opened by the header ☰; RTL slides from the
      // right). Detail/secondary pages inherit it as they migrate onto KsaPage.
      drawer: const MoreDrawer(),
      bottomNavigationBar: SimfBottomNav(current: tab),
      body: Stack(
        children: <Widget>[
          if (showSweep) const KsaSweep(),
          SafeArea(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                if (headerRow != null) headerRow,
                Expanded(child: body),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _defaultHeader(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          // Leading: the back chevron on pushed pages; nothing on a tab root
          // (the ☰ in the trailing controller opens the menu instead).
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
          // The shared top controller — notifications + the drawer ☰, the same
          // on every shell page. The ☰ opens the side menu via the nearest
          // Scaffold (the Builder gives a context below it).
          IconButton(
            tooltip: l10n.notificationsTooltip,
            onPressed: () => context.pushNamed(RouteNames.notifications),
            icon: const Icon(
              Icons.notifications_none_outlined,
              color: Colors.white,
              size: 22,
            ),
          ),
          Builder(
            builder: (ctx) => KsaMenuButton(
              onTap: () => Scaffold.of(ctx).openDrawer(),
            ),
          ),
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

/// The circled drawer ☰ control (same dark circle as [KsaBackButton]) — opens
/// the shell's side menu. Lives in the shared header's trailing controller.
class KsaMenuButton extends StatelessWidget {
  const KsaMenuButton({required this.onTap, super.key});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      onPressed: onTap,
      tooltip: MaterialLocalizations.of(context).openAppDrawerTooltip,
      style: IconButton.styleFrom(
        backgroundColor: SimfTokens.navyDeep,
        shape: const CircleBorder(),
      ),
      icon: const Icon(Icons.menu, color: Colors.white, size: 20),
    );
  }
}

/// The decorative rotated sweep block from the KSA entry frames (28.28°,
/// white-4% fill) — the single home for the transform (the auth chrome
/// consumes it too).
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
          decoration: const BoxDecoration(
            color: SimfTokens.surfaceTint,
            borderRadius: BorderRadius.all(Radius.circular(40)),
          ),
        ),
      ),
    );
  }
}

/// The W2 base card chrome — the `navyDeep` fill with the beige hairline on
/// the small radius — shared by every tile/row in the batch so the border /
/// fill / radius have one owner. [onTap] null renders a plain surface.
class KsaCard extends StatelessWidget {
  const KsaCard({
    required this.child,
    this.onTap,
    this.color = SimfTokens.navyDeep,
    this.borderColor = SimfTokens.beigeBorder,
    this.borderWidth = SimfTokens.hairline,
    super.key,
  });

  final Widget child;
  final VoidCallback? onTap;
  final Color color;
  final Color borderColor;
  final double borderWidth;

  static const BorderRadius _radius =
      BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

  @override
  Widget build(BuildContext context) {
    return Material(
      color: color,
      shape: RoundedRectangleBorder(
        borderRadius: _radius,
        side: BorderSide(color: borderColor, width: borderWidth),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: _radius,
        child: child,
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
            borderRadius:
                const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
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

/// A row of equally sized tiles with the standard gap between them — the
/// frames' 2- and 3-up tile rows (home sections, profile grid).
class KsaTileRow extends StatelessWidget {
  const KsaTileRow({required this.children, super.key});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        for (final (index, child) in children.indexed) ...<Widget>[
          if (index > 0) const SizedBox(width: SimfTokens.space2),
          Expanded(child: child),
        ],
      ],
    );
  }
}

/// One navy feature tile (frame tiles "المتحدثون" / "الجلسات" / …): a 72-high
/// card with a gold icon over a small white label. [enabled] false renders
/// the locked variant (the "بطاقتي" card / the disabled theme tile) on the
/// disabled palette with no tap.
class KsaNavTile extends StatelessWidget {
  const KsaNavTile({
    required this.label,
    required this.icon,
    this.onTap,
    this.enabled = true,
    super.key,
  });

  final String label;
  final IconData icon;
  final VoidCallback? onTap;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final foreground =
        enabled ? SimfTokens.accent : SimfTokens.navyDisabledText;
    final labelColor = enabled ? Colors.white : SimfTokens.navyDisabledText;
    return KsaCard(
      onTap: enabled ? onTap : null,
      color: enabled ? SimfTokens.navyDeep : SimfTokens.navyDisabled,
      borderColor: enabled
          ? SimfTokens.beigeBorder
          : SimfTokens.navyDisabledBorder,
      borderWidth: enabled ? SimfTokens.hairline : 1,
      child: _TileBody(
        top: Icon(icon, size: 24, color: foreground),
        label: label,
        labelColor: labelColor,
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
    return KsaCard(
      child: _TileBody(
        top: Text(
          '$value',
          style: const TextStyle(
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w700,
            color: SimfTokens.accent,
          ),
        ),
        label: label,
        labelColor: Colors.white,
      ),
    );
  }
}

/// The shared tile interior: a centred top element over the small bold label.
class _TileBody extends StatelessWidget {
  const _TileBody({
    required this.top,
    required this.label,
    required this.labelColor,
  });

  final Widget top;
  final String label;
  final Color labelColor;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(minHeight: 72),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            top,
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
          ],
        ),
      ),
    );
  }
}

/// The gold rounded-square avatar (home header 203:1238 / profile identity
/// cards): the photo when [imageUrl] is set (falling back on error), else the
/// name's initials on gold.
class KsaAvatar extends StatelessWidget {
  const KsaAvatar({
    required this.name,
    this.imageUrl,
    this.size = 42,
    this.initialsBackground = SimfTokens.accent,
    this.initialsForeground = SimfTokens.navy,
    super.key,
  });

  final String name;
  final String? imageUrl;
  final double size;

  /// The initials-fallback box colours. The badge identity strip is gold, so it
  /// overrides these to a white box with navy text (the default gold-on-gold
  /// would vanish there).
  final Color initialsBackground;
  final Color initialsForeground;

  @override
  Widget build(BuildContext context) {
    final initials = _Initials(
      name: name,
      size: size,
      background: initialsBackground,
      foreground: initialsForeground,
    );
    return ClipRRect(
      borderRadius: const BorderRadius.all(Radius.circular(SimfTokens.radius)),
      child: SizedBox(
        width: size,
        height: size,
        child: imageUrl == null
            ? initials
            : Image.network(
                imageUrl!,
                fit: BoxFit.cover,
                errorBuilder: (_, __, ___) => initials,
              ),
      ),
    );
  }
}

class _Initials extends StatelessWidget {
  const _Initials({
    required this.name,
    required this.size,
    required this.background,
    required this.foreground,
  });

  final String name;
  final double size;
  final Color background;
  final Color foreground;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: background,
      child: Center(
        child: Text(
          ksaInitials(name),
          style: TextStyle(
            color: foreground,
            fontWeight: FontWeight.w700,
            fontSize: size * 0.33,
          ),
        ),
      ),
    );
  }
}

/// The first letters of the first + last name parts (`·` when empty) — the
/// avatar fallback used wherever no photo is available.
String ksaInitials(String name) {
  final parts =
      name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  String first(String s) =>
      s.isEmpty ? '' : String.fromCharCode(s.runes.first).toUpperCase();
  if (parts.isEmpty) {
    return '·';
  }
  if (parts.length == 1) {
    return first(parts.first);
  }
  return first(parts.first) + first(parts.last);
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
    return KsaCard(
      onTap: onTap,
      borderColor: SimfTokens.goldSoft,
      borderWidth: SimfTokens.hairlineBold,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            if (badge != null) ...<Widget>[
              Container(
                width: 72,
                height: 64,
                alignment: Alignment.center,
                decoration: const BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius:
                      BorderRadius.all(Radius.circular(SimfTokens.radius)),
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
    );
  }
}

/// The standard error surface: the message over a gold retry button — one
/// home for the retry chrome instead of a per-screen copy.
class KsaErrorState extends StatelessWidget {
  const KsaErrorState({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
    super.key,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(retryLabel)),
          ],
        ),
      ),
    );
  }
}

/// The standard empty / pending surface: a muted icon over the message.
class KsaEmptyState extends StatelessWidget {
  const KsaEmptyState({required this.icon, required this.message, super.key});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 56, color: SimfTokens.beigeBorder),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.beigeBorder),
            ),
          ],
        ),
      ),
    );
  }
}
