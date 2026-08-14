import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/more_drawer.dart';
import 'package:simf_app/app/widgets/screen_announcer.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_language_toggle.dart';
import 'package:simf_app/app/widgets/simf_nav_controls.dart';
import 'package:simf_app/app/widgets/simf_sweep_background.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart'
    show unreadNotificationCountProvider;

// One widget group per file (CLAUDE.md §1). Re-exported here so the ~489
// existing `simf_page_shell.dart` imports across the app keep resolving.
export 'simf_avatar.dart';
export 'simf_cards.dart';
export 'simf_nav_controls.dart';
export 'simf_refresh.dart';
export 'simf_states.dart';
export 'simf_sweep_background.dart';
export 'simf_tiles.dart';

/// Shared KSA main-shell chrome for the Wave-2 in-app pages (frames
/// 512:1492 / 203:1236 / 512:1780 / 215:767 / 221:769 / 215:562): the navy
/// page scaffold with the standard header, plus the card / tile / list-row /
/// state-surface building blocks every page composes. One widget per
/// repeated frame element — pages never copy-paste shell markup.

/// The navy page scaffold: background, optional decorative sweep, a
/// forced-LTR header (circled back chevron at the left, centred title — the
/// D-363 chrome pattern), the page [body], and the shared bottom nav.
///
/// Pages with a non-standard header (e.g. the signed-in home's greeting row)
/// pass [header] instead of [title]; with no [header], [title] and [onBack]
/// the header row collapses entirely (full-bleed pages like the map).
/// [onBack] null hides the back button.
class SimfPageShell extends StatelessWidget {
  const SimfPageShell({
    required this.body,
    this.title,
    this.header,
    this.onBack,
    this.tab,
    this.showSweep = false,
    this.showBottomNav = true,
    this.showNotificationsBell = true,
    this.showHeaderActions = false,
    this.showLanguageToggle = true,
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

  /// Whether the bottom navigation bar is rendered. Pages that precede role
  /// selection (e.g. guest-mode screen) pass false.
  final bool showBottomNav;

  /// Whether the default header's action cluster shows the notifications bell.
  /// True on every signed-in surface; the guest home (frame 758:2910) passes
  /// false — a guest has no personal notifications.
  final bool showNotificationsBell;

  /// Whether the default header shows the trailing action cluster (bell /
  /// language / theme / menu). Defaults to **false** (owner 2026-06-28): the
  /// Figma standard sub-page nav (758-1469 / 922-2824) is back + centred title
  /// + bottom hairline only, and a 42-wide spacer balances the back box to keep
  /// the title centred. The cluster lives on the Home greeting header (which
  /// builds its own [header]); a page may pass true to opt back in.
  final bool showHeaderActions;

  /// Whether the lone trailing language toggle shows on the default header.
  /// The المزيد main menu (frame 1129:17224) passes false: it already carries a
  /// language *row* inside the menu, so the header pill is redundant (owner
  /// 2026-07-07). Ignored when [showHeaderActions] is true (that cluster owns
  /// its own toggle). When both are false a 42-wide spacer keeps the title
  /// centred against the back box.
  final bool showLanguageToggle;

  @override
  Widget build(BuildContext context) {
    final headerRow = header ??
        (title == null && onBack == null ? null : _defaultHeader(context));
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      // The shared shell's side menu — the same المزيد drawer on every page
      // that uses this scaffold (opened by the header ☰; RTL slides from the
      // right). Detail/secondary pages inherit it as they migrate onto SimfPageShell.
      drawer: const MoreDrawer(),
      bottomNavigationBar: showBottomNav ? SimfBottomNav(current: tab) : null,
      body: Stack(
        children: <Widget>[
          if (showSweep) const SimfSweepBackground(),
          // Page-038 screen-reader assist: announces this page's title once on
          // mount when the user has enabled it (invisible; self-guards).
          ScreenAnnouncer(title: title),
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
    // Figma standard top-nav (758-1469 / 922-2824): a fixed-height header with a
    // bottom hairline, a 42×42 navy back box at the inline start, and a centred
    // 18px SemiBold title. Reused across every inner page.
    return Container(
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
        ),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        // Forced LTR to match the Figma sub-page frames (758-1469 / 922-2824 /
        // 908-1744 …): the back box sits on the LEFT and the title is centred,
        // even under Arabic/RTL (owner 2026-06-28 "match figma in sub page nav";
        // supersedes the 2026-06-18 natural-direction header). The title Text
        // still renders its own RTL content.
        textDirection: TextDirection.ltr,
        children: <Widget>[
          // Leading: the 42×42 navy back box (Figma 758:1473) on the LEFT; the
          // chevron is not mirrored (the frame's chevron points left).
          SizedBox(
            width: SimfTokens.simfPageShellWidthSm,
            height: SimfTokens.simfPageShellHeightSm,
            child:
                onBack == null ? null : SimfCircledBackButton(onBack: onBack!),
          ),
          Expanded(
            child: Text(
              title ?? '',
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              // Figma sub-page headers: 18px / SemiBold (was 20 / w500).
              style: SimfTokens.labelWhiteSemiboldTitle,
            ),
          ),
          // The shared trailing action cluster (bell + language + drawer ☰)
          // when [showHeaderActions]; otherwise the gold language
          // globe alone — the Figma sub-page frames (908-1744 …) carry it at the
          // trailing corner (owner 2026-07-05: add the globe to every sub-page,
          // superseding the 2026-06-28 back+title-only nav). Its 40-wide box also
          // balances the 42 back box so the title stays effectively centred.
          if (showHeaderActions)
            SimfHeaderActions(showBell: showNotificationsBell)
          else if (showLanguageToggle)
            Consumer(
              builder: (context, ref, _) => SimfLanguageToggle(
                onPressed: () => unawaited(
                  ref.read(localeControllerProvider.notifier).toggle(),
                ),
              ),
            )
          else
            // المزيد (1129:17224) drops the header pill; the spacer balances the
            // 42-wide back box so the title stays centred.
            const SizedBox(
                width: SimfTokens.simfPageShellWidthSm,
                height: SimfTokens.simfPageShellHeightSm,),
        ],
      ),
    );
  }
}

/// The shared trailing action cluster on every in-app page's top nav (owner
/// 2026-06-27): the notifications bell and the menu ☰ — each a **gold glyph in
/// a navy rounded box** (frame 758:1136), so the top nav is identical on the
/// signed-in home greeting header and every [SimfPageShell] sub-page.
///
/// The language pill is **not a member of this cluster** (owner 2026-07-11): a
/// sub-page gets its own pill from [SimfPageShell]'s trailing slot instead.
/// That call assumed every surface had a sub-page header to fall back on, which
/// the signed-in Home does not — it builds its own greeting header, so Home was
/// left with no route to the language switch at all (BUG-017). The owner
/// reversed the Home half of that call on **2026-07-27** ("keep home lang",
/// D-772):
/// `GreetingHeader` renders a [SimfLanguageToggle] as a **sibling beside** this
/// cluster. Every other surface is unchanged — do NOT move the pill back inside
/// the cluster, or Home would render two of them.
///
/// [showBell] is true on every signed-in surface; the guest home (frame
/// 758:2910) sets it false — a guest has no personal notifications.
///
/// [showUnreadBadge] gates the live unread-count badge. Only the signed-in home
/// greeting header turns it on — that surface resolves the auth-scoped count
/// provider. Sub-pages keep the bell as a plain navigation control (no badge),
/// so a generic [SimfPageShell] never has to provide the auth/notifications wiring
/// just to render its header.
class SimfHeaderActions extends ConsumerWidget {
  const SimfHeaderActions({
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
          Builder(
            builder: (ctx) => _box(
              // BUG-017 — this control opens the side drawer, a different menu
              // from the Profile "More" hub; both used to announce as "More".
              tooltip: l10n.menuTitle,
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
