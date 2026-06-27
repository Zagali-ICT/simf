import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/accessibility/data/accessibility_controller.dart';
import '../../features/notifications/data/notifications_repository.dart'
    show unreadNotificationCountProvider;
import '../../features/profile/data/profile_repository.dart'
    show myAvatarBytesProvider;
import '../localization/app_l10n.dart';
import '../localization/locale_controller.dart';
import '../route_names.dart';
import '../theme/tokens.dart';
import 'more_drawer.dart';
import 'simf_bottom_nav.dart';
import 'simf_logo.dart';
import 'simf_svg_icon.dart';

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

/// A standard AppBar leading back button for raw-`AppBar` screens. Always shows
/// (unlike the auto-leading, which vanishes when the screen is the navigator
/// root after a resume / deep-link, trapping the user) and always works:
/// [ksaBackOrHome] pops when it can, else lands on home. Use as
/// `appBar: AppBar(leading: const SimfBackButton(), ...)`. (D-426)
class SimfBackButton extends StatelessWidget {
  const SimfBackButton({super.key});

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: const Icon(Icons.arrow_back),
      tooltip: MaterialLocalizations.of(context).backButtonTooltip,
      onPressed: () => ksaBackOrHome(context),
    );
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
    this.showNotificationsBell = true,
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

  /// Whether the default header's action cluster shows the notifications bell.
  /// True on every signed-in surface; the guest home (frame 758:2910) passes
  /// false — a guest has no personal notifications.
  final bool showNotificationsBell;

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
          // Page-038 screen-reader assist: announces this page's title once on
          // mount when the user has enabled it (invisible; self-guards).
          _ScreenAnnouncer(title: title),
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
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        // Natural direction (owner 2026-06-18): the leading control sits at the
        // inline START (physical right under RTL, left under LTR) and the
        // trailing controls at the END — no forced LTR, so the bar mirrors with
        // the locale.
        children: <Widget>[
          // Leading: the back chevron on pushed pages; nothing on a tab root
          // (the ☰ in the trailing controller opens the menu instead).
          SizedBox(
            width: 40,
            height: 40,
            child: onBack == null
                ? null
                : KsaBackButton(onBack: onBack!, mirrorInRtl: true),
          ),
          Expanded(
            child: Text(
              title ?? '',
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              // Figma sub-page headers: 18px / SemiBold (was 20 / w500).
              style: const TextStyle(
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w600,
                color: Colors.white,
              ),
            ),
          ),
          // The shared trailing action cluster — the SAME top-nav controls on
          // every page (owner 2026-06-27): the notifications bell + its unread
          // badge, the language globe, the inert dark-mode crescent, and the
          // drawer ☰. One component so every page's top nav is identical.
          KsaHeaderActions(showBell: showNotificationsBell),
        ],
      ),
    );
  }
}

/// Announces the page [title] once on mount through the platform accessibility
/// channel, but only when the Page-038 screen-reader assist is enabled. Renders
/// nothing; lives invisibly in the [KsaPage] stack so every shell page that
/// carries a title participates without per-screen wiring.
class _ScreenAnnouncer extends ConsumerStatefulWidget {
  const _ScreenAnnouncer({required this.title});

  final String? title;

  @override
  ConsumerState<_ScreenAnnouncer> createState() => _ScreenAnnouncerState();
}

class _ScreenAnnouncerState extends ConsumerState<_ScreenAnnouncer> {
  @override
  void initState() {
    super.initState();
    final title = widget.title?.trim();
    if (title == null || title.isEmpty) {
      return;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      bool assist;
      try {
        assist = ref.read(accessibilityControllerProvider).screenReaderAssist;
      } catch (_) {
        // Accessibility DI not wired (e.g. a widget test that builds a KsaPage
        // without overriding the controller). The announcer is best-effort and
        // must never break a page, so skip silently.
        return;
      }
      if (!assist) {
        return;
      }
      final l10n = AppL10n.of(context);
      SemanticsService.sendAnnouncement(
        View.of(context),
        l10n.accessibilityScreenAnnouncement(title),
        Directionality.of(context),
      );
    });
  }

  @override
  Widget build(BuildContext context) => const SizedBox.shrink();
}

/// The circled back chevron (dark circle, white chevron). By default the glyph
/// always points left — the D-363 forced-LTR header pattern still used by the
/// speakers / session-detail / speaker-profile headers. In the shared
/// natural-direction [KsaPage] header [mirrorInRtl] is set, so the chevron
/// mirrors to point right under RTL, where the leading control sits at the
/// inline start (physical right).
class KsaBackButton extends StatelessWidget {
  const KsaBackButton({
    required this.onBack,
    this.mirrorInRtl = false,
    super.key,
  });

  final VoidCallback onBack;

  /// Mirror the chevron horizontally under RTL. Only the natural-direction
  /// shell header opts in; forced-LTR headers keep the always-left chevron.
  final bool mirrorInRtl;

  @override
  Widget build(BuildContext context) {
    final flip = mirrorInRtl && Directionality.of(context) == TextDirection.rtl;
    return IconButton(
      onPressed: onBack,
      style: IconButton.styleFrom(
        backgroundColor: SimfTokens.navyDeep,
        shape: const CircleBorder(),
      ),
      // Figma frames use the iconamoon chevron, not a Material back-arrow.
      icon: Transform.flip(
        flipX: flip,
        child: const SimfSvgIcon(
          'assets/icons/ic_back.svg',
          size: 24,
          color: Colors.white,
        ),
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

/// The shared header actions added to every in-app app-bar — the standard
/// [KsaPage] header and the home greeting header: the language globe (toggles
/// AR ↔ EN, persisted via [LocaleController]) and the dark-mode icon.
///
/// Dark mode is intentionally **inert** for now: the app is navy-always (the
/// owner's design decision) and has no light theme yet, so the icon is shown
/// for parity but disabled — mirroring the side-drawer's inert theme tile. The
/// bell + drawer ☰ stay owned by each header.
///
/// The locale notifier is read **only in the onPressed callback** (never
/// `watch`ed at build), so this control adds no build-time dependency on
/// [localeControllerProvider]: screens render without overriding it in tests.
/// (Tapping the globe does resolve the provider, so a test that exercises the
/// toggle must still override it — see ksa_shell_test.)
class KsaLangThemeButtons extends ConsumerWidget {
  const KsaLangThemeButtons({this.size = 22, super.key});

  /// The glyph size — 22 in the standard header, 26 in the larger home header.
  final double size;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        IconButton(
          tooltip: l10n.languageToggleLabel,
          onPressed: () =>
              unawaited(ref.read(localeControllerProvider.notifier).toggle()),
          icon: Icon(Icons.language, color: Colors.white, size: size),
        ),
        IconButton(
          tooltip: l10n.themeToggleTooltip,
          // Node 1049:2087 — the gold crescent (iconamoon:mode-dark-fill).
          // Intentionally inert: the app is navy-always (owner decision), no
          // light theme yet, so the icon shows for parity but does nothing.
          onPressed: null,
          icon: Icon(
            Icons.dark_mode,
            color: SimfTokens.accent,
            size: size,
          ),
        ),
      ],
    );
  }
}

/// The shared trailing action cluster on every in-app page's top nav (owner
/// 2026-06-27): the notifications bell, the language globe, the inert dark-mode
/// crescent (node 1049:2087) and the menu ☰ — each a **gold glyph in a navy
/// rounded box** (frame 758:1136), so the top nav is identical on the signed-in
/// home greeting header and every [KsaPage] sub-page.
///
/// [showBell] is true on every signed-in surface; the guest home (frame
/// 758:2910) sets it false — a guest has no personal notifications.
///
/// [showUnreadBadge] gates the live unread-count badge. Only the signed-in home
/// greeting header turns it on — that surface resolves the auth-scoped count
/// provider. Sub-pages keep the bell as a plain navigation control (no badge),
/// so a generic [KsaPage] never has to provide the auth/notifications wiring
/// just to render its header.
class KsaHeaderActions extends ConsumerWidget {
  const KsaHeaderActions({
    this.size = 34,
    this.showBell = true,
    this.showUnreadBadge = false,
    super.key,
  });

  /// The action box edge length. Frame 758:1136 draws 42-px boxes for two
  /// icons; with all four the boxes shrink to 34 so they fit beside the
  /// greeting + avatar (owner 2026-06-27).
  final double size;

  /// Whether the notifications bell shows. False on the guest home (758:2910).
  final bool showBell;

  /// Whether the bell carries the live unread-count badge (home only).
  final bool showUnreadBadge;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    // Best-effort unread count — watched only where the badge shows (the home
    // greeting header), so a generic page's header never depends on the
    // auth-scoped count provider. Any wire error resolves to 0.
    final unread = showBell && showUnreadBadge
        ? ref.watch(unreadNotificationCountProvider).maybeWhen(
              data: (count) => count,
              orElse: () => 0,
            )
        : 0;
    Widget bellGlyph = const Icon(Icons.notifications_none_outlined);
    if (showUnreadBadge && unread > 0) {
      bellGlyph = Badge.count(count: unread, child: bellGlyph);
    }
    // The frame lays the boxes left→right (bell … menu); force LTR so the order
    // is stable under either locale (like the social row).
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (showBell) ...<Widget>[
            _box(
              tooltip: l10n.notificationsTooltip,
              onTap: () => context.pushNamed(RouteNames.notifications),
              glyph: bellGlyph,
            ),
            const SizedBox(width: SimfTokens.space2),
          ],
          _box(
            tooltip: l10n.languageToggleLabel,
            onTap: () =>
                unawaited(ref.read(localeControllerProvider.notifier).toggle()),
            glyph: const Icon(Icons.language),
          ),
          const SizedBox(width: SimfTokens.space2),
          // Node 1049:2087 — the gold crescent, intentionally inert (navy-always).
          _box(
            tooltip: l10n.themeToggleTooltip,
            onTap: null,
            glyph: const Icon(Icons.dark_mode),
          ),
          const SizedBox(width: SimfTokens.space2),
          Builder(
            builder: (ctx) => _box(
              tooltip: l10n.moreTitle,
              onTap: () => Scaffold.of(ctx).openDrawer(),
              glyph: const Icon(Icons.menu),
            ),
          ),
        ],
      ),
    );
  }

  /// One header action box: a gold glyph centred in a navy rounded square with
  /// the beige hairline (frame 758:1136). Kept as an [IconButton] so tooltips +
  /// the disabled (inert) state stay correct.
  Widget _box({
    required String tooltip,
    required VoidCallback? onTap,
    required Widget glyph,
  }) {
    return IconButton(
      tooltip: tooltip,
      onPressed: onTap,
      iconSize: size * 0.55,
      icon: glyph,
      style: IconButton.styleFrom(
        backgroundColor: SimfTokens.navyDeep,
        disabledBackgroundColor: SimfTokens.navyDeep,
        foregroundColor: SimfTokens.accent,
        disabledForegroundColor: SimfTokens.accent,
        fixedSize: Size(size, size),
        minimumSize: Size(size, size),
        maximumSize: Size(size, size),
        padding: EdgeInsets.zero,
        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
          side: BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
        ),
      ),
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
    final BorderRadius br = BorderRadius.all(Radius.circular(radius));
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
                // Frame 758:1134 — the "more" link is white, Medium.
                style: const TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w500,
                  color: Colors.white,
                ),
              ),
            ),
          ),
      ],
    );
  }
}

/// A bordered single-line link row — the signed-in home's section bars
/// (frames 758:1207 / 1049:12844 / 758:1211 "عن الملتقى" / "الرعاة" /
/// "الأخبار والتغطية"): a transparent 48-high box with the beige hairline, the
/// title at the inline end (physical right under RTL) and a gold caret at the
/// inline start. Tappable. Distinct from [KsaSectionHeader] (a plain text label)
/// and [KsaListRow] (which carries a gold badge box + subtitle).
class KsaLinkRow extends StatelessWidget {
  const KsaLinkRow({required this.title, required this.onTap, super.key});

  final String title;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
      color: Colors.transparent,
      borderColor: SimfTokens.beigeBorder,
      borderWidth: SimfTokens.hairline,
      child: SizedBox(
        height: 48,
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
                  style: const TextStyle(
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w500,
                    color: Colors.white,
                  ),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              // The frame's gold left-caret (eva:arrow-up-fill rotated). The
              // bundled SVG does not auto-mirror under RTL, so it stays pointing
              // left as the design shows (same as [KsaListRow]).
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
    this.icon,
    this.iconAsset,
    this.onTap,
    this.enabled = true,
    this.minHeight = 72,
    super.key,
  }) : assert(
          icon != null || iconAsset != null,
          'KsaNavTile needs either a Material icon or an SVG iconAsset.',
        );

  final String label;

  /// A Material glyph (the default tile icon source).
  final IconData? icon;

  /// An optional bundled SVG asset path (e.g. the KSA frame's exact iconify
  /// glyph). When set it is rendered tinted to the tile colour and takes
  /// precedence over [icon].
  final String? iconAsset;

  final VoidCallback? onTap;
  final bool enabled;

  /// The tile's minimum height. The frame uses 72 for the "عن الملتقى" row and
  /// 80 for the news + smart-feature rows (758:1216 vs 758:1164).
  final double minHeight;

  @override
  Widget build(BuildContext context) {
    final foreground =
        enabled ? SimfTokens.accent : SimfTokens.navyDisabledText;
    final labelColor = enabled ? Colors.white : SimfTokens.navyDisabledText;
    final asset = iconAsset;
    final Widget top = asset != null
        ? SimfSvgIcon(asset, size: 24, color: foreground)
        : Icon(icon, size: 24, color: foreground);
    return KsaCard(
      onTap: enabled ? onTap : null,
      color: enabled ? SimfTokens.navyDeep : SimfTokens.navyDisabled,
      borderColor: enabled
          ? SimfTokens.beigeBorder
          : SimfTokens.navyDisabledBorder,
      borderWidth: enabled ? SimfTokens.hairline : 1,
      child: _TileBody(
        top: top,
        label: label,
        labelColor: labelColor,
        minHeight: minHeight,
      ),
    );
  }
}

/// A stat tile (frames 512:1780 / 213:963): a big gold number over its label,
/// on the same card chrome as [KsaNavTile].
class KsaStatTile extends StatelessWidget {
  const KsaStatTile({
    required this.value,
    required this.label,
    this.onTap,
    super.key,
  });

  final int value;
  final String label;

  /// Optional tap target. Null (the default) keeps the tile inert — a plain
  /// statistic; non-null makes the whole card tappable via [KsaCard]'s InkWell.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
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
    this.minHeight = 72,
  });

  final Widget top;
  final String label;
  final Color labelColor;
  final double minHeight;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(minHeight: minHeight),
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
/// cards). When [currentUser] is true it shows the **signed-in user's** photo,
/// fetched as authenticated bytes via [myAvatarBytesProvider] (the avatar
/// endpoint is bearer-gated and behind self-signed TLS, so a bare
/// `Image.network` can't load it) and refreshed immediately after an upload via
/// the avatar bust token. Otherwise — and whenever no photo is available — it
/// renders the brand-mark fallback. [name] drives the accessibility label only.
class KsaAvatar extends ConsumerWidget {
  const KsaAvatar({
    required this.name,
    this.currentUser = false,
    this.size = 42,
    super.key,
  });

  final String name;

  /// True for the signed-in user's own avatar (home / badge / my-area). False
  /// for any other person's avatar (e.g. a question submitter) — that always
  /// shows the fallback, never the signed-in user's photo.
  final bool currentUser;
  final double size;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fallback = _AvatarFallback(size: size);
    Widget child = fallback;
    if (currentUser) {
      final bytes = ref.watch(myAvatarBytesProvider).asData?.value;
      if (bytes != null && bytes.isNotEmpty) {
        child = Image.memory(
          bytes,
          fit: BoxFit.cover,
          gaplessPlayback: true,
          errorBuilder: (_, __, ___) => fallback,
        );
      }
    }
    return Semantics(
      image: true,
      label: name.trim().isEmpty ? null : name,
      child: ClipRRect(
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radius)),
        child: SizedBox(width: size, height: size, child: child),
      ),
    );
  }
}

/// The default avatar when a user has no photo (or the photo fails to load):
/// the SIMF brand mark on a navy box. The owner chose the logo over a cultural
/// figure (D-402); the logo art is white, so the box stays dark (navyDeep) for
/// contrast on every surface — the navy scaffold, the my-area card, and the
/// gold badge strip alike.
class _AvatarFallback extends StatelessWidget {
  const _AvatarFallback({required this.size});

  final double size;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.navyDeep,
      child: Padding(
        padding: EdgeInsets.all(size * 0.18),
        child: SimfLogo(size: size * 0.64),
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
            // Frame 758:1274 — a gold left-pointing caret. Material's
            // Icons.arrow_left auto-mirrors to the right under RTL; the bundled
            // SVG does not, so it stays pointing left as the design shows.
            const SimfSvgIcon(
              'assets/icons/ic_caret_left.svg',
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
