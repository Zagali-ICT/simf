import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/accessibility/data/accessibility_controller.dart';
import '../../features/account/data/profile_repository.dart'
    show myAvatarBytesProvider;
import '../../features/notifications/data/notifications_repository.dart'
    show unreadNotificationCountProvider;
import '../localization/app_l10n.dart';
import '../localization/locale_controller.dart';
import '../route_names.dart';
import '../theme/app_assets.dart';
import '../theme/tokens.dart';
import 'more_drawer.dart';
import 'simf_app_shell.dart' show SimfShellScope, tabIndex;
import 'simf_bottom_nav.dart';
import 'simf_image_viewer.dart';
import 'simf_language_toggle.dart';
import 'simf_logo.dart';
import 'simf_svg_icon.dart';

// One widget group per file (CLAUDE.md §1). Re-exported here so the ~489
// existing `simf_page_shell.dart` imports across the app keep resolving.
export 'simf_cards.dart';
export 'simf_refresh.dart';
export 'simf_states.dart';
export 'simf_tiles.dart';

/// Shared KSA main-shell chrome for the Wave-2 in-app pages (frames
/// 512:1492 / 203:1236 / 512:1780 / 215:767 / 221:769 / 215:562): the navy
/// page scaffold with the standard header, plus the card / tile / list-row /
/// state-surface building blocks every page composes. One widget per
/// repeated frame element — pages never copy-paste shell markup.

/// The standard back action: pop a pushed route when possible; otherwise, if
/// we are one of the in-shell bottom-nav tabs (Agenda / Badge / Profile), switch
/// the shell back to the Home tab; else navigate home.
///
/// The in-shell tabs never leave the shell's `/` location, so `context.canPop()`
/// is false and a bare `goNamed(home)` navigates to `/` while already at `/` —
/// a no-op that leaves the back chevron dead. Switching the shell tab is what
/// actually returns those tabs to Home (mirrors the bottom nav's `_shellOrGo`).
void backOrHome(BuildContext context) {
  if (context.canPop()) {
    context.pop();
    return;
  }
  final shell = SimfShellScope.maybeOf(context);
  if (shell != null) {
    shell.switchTab(tabIndex(SimfTab.home));
    return;
  }
  context.goNamed(RouteNames.home);
}

/// A standard AppBar leading back button for raw-`AppBar` screens. Always shows
/// (unlike the auto-leading, which vanishes when the screen is the navigator
/// root after a resume / deep-link, trapping the user) and always works:
/// [backOrHome] pops when it can, else lands on home. Use as
/// `appBar: AppBar(leading: const SimfBackButton(), ...)`. (D-426)
class SimfBackButton extends StatelessWidget {
  const SimfBackButton({super.key});

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: const Icon(Icons.arrow_back),
      tooltip: MaterialLocalizations.of(context).backButtonTooltip,
      onPressed: () => backOrHome(context),
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
      bottomNavigationBar:
          showBottomNav ? SimfBottomNav(current: tab) : null,
      body: Stack(
        children: <Widget>[
          if (showSweep) const SimfSweepBackground(),
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
            width: 42,
            height: 42,
            child: onBack == null
                ? null
                : SimfCircledBackButton(onBack: onBack!, mirrorInRtl: false),
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
            const SizedBox(width: 42, height: 42),
        ],
      ),
    );
  }
}

/// Announces the page [title] once on mount through the platform accessibility
/// channel, but only when the Page-038 screen-reader assist is enabled. Renders
/// nothing; lives invisibly in the [SimfPageShell] stack so every shell page that
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
        // Accessibility DI not wired (e.g. a widget test that builds a SimfPageShell
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
/// natural-direction [SimfPageShell] header [mirrorInRtl] is set, so the chevron
/// mirrors to point right under RTL, where the leading control sits at the
/// inline start (physical right).
class SimfCircledBackButton extends StatelessWidget {
  const SimfCircledBackButton({
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
      // The chevron is an SVG with no text, so without this the control had no
      // accessible name at all — a screen reader announced a bare "button" on
      // the ~18 screens that carry the shared header (BUG-003). The localized
      // Material string keeps it bilingual with no new l10n key.
      tooltip: MaterialLocalizations.of(context).backButtonTooltip,
      style: IconButton.styleFrom(
        backgroundColor: SimfTokens.navyDeep,
        shape: const CircleBorder(),
      ),
      // Figma frames use the iconamoon chevron, not a Material back-arrow.
      icon: Transform.flip(
        flipX: flip,
        child: const SimfSvgIcon(
          AppAssets.icBack,
          size: 24,
          color: SimfTokens.surface,
        ),
      ),
    );
  }
}

/// The circled drawer ☰ control (same dark circle as [SimfCircledBackButton]) — opens
/// the shell's side menu. Lives in the shared header's trailing controller.
class SimfMenuButton extends StatelessWidget {
  const SimfMenuButton({required this.onTap, super.key});

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
      icon: const Icon(Icons.menu, color: SimfTokens.surface, size: 20),
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

/// The decorative rotated sweep block from the KSA entry frames (28.28°,
/// white-4% fill) — the single home for the transform (the auth chrome
/// consumes it too).
class SimfSweepBackground extends StatelessWidget {
  const SimfSweepBackground({super.key});

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

/// The gold rounded-square avatar (home header 203:1238 / profile identity
/// cards). When [currentUser] is true it shows the **signed-in user's** photo,
/// fetched as authenticated bytes via [myAvatarBytesProvider] (the avatar
/// endpoint is bearer-gated and behind self-signed TLS, so a bare
/// `Image.network` can't load it) and refreshed immediately after an upload via
/// the avatar bust token. Otherwise — and whenever no photo is available — it
/// renders the brand-mark fallback. [name] drives the accessibility label only.
///
/// Owner 2026-07-26 — set [enableFullScreen] on a DISPLAY-ONLY photo (the badge
/// card) so tapping it opens the picture full size from the already-fetched
/// bytes (D-422: the avatar endpoint is bearer-gated, so the viewer paints a
/// [MemoryImage], never a bare `Image.network`). It stays off where the tap
/// already means something else (My-Area's change-photo affordance).
class SimfAvatar extends ConsumerWidget {
  const SimfAvatar({
    required this.name,
    this.currentUser = false,
    this.size = 42,
    this.enableFullScreen = false,
    super.key,
  });

  final String name;

  /// True for the signed-in user's own avatar (home / badge / my-area). False
  /// for any other person's avatar (e.g. a question submitter) — that always
  /// shows the fallback, never the signed-in user's photo.
  final bool currentUser;
  final double size;

  /// Opens the photo full size on tap. Only honoured when a real photo is
  /// shown — the brand-mark fallback has nothing to enlarge.
  final bool enableFullScreen;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fallback = _AvatarFallback(size: size);
    Widget child = fallback;
    MemoryImage? photo;
    if (currentUser) {
      final bytes = ref.watch(myAvatarBytesProvider).asData?.value;
      if (bytes != null && bytes.isNotEmpty) {
        photo = MemoryImage(bytes);
        child = Image(
          image: photo,
          fit: BoxFit.cover,
          gaplessPlayback: true,
          errorBuilder: (_, __, ___) => fallback,
        );
      }
    }
    final label = name.trim().isEmpty ? null : name;
    final box = ClipRRect(
      borderRadius: const BorderRadius.all(Radius.circular(SimfTokens.radius)),
      child: SizedBox(width: size, height: size, child: child),
    );
    if (!enableFullScreen || photo == null) {
      return Semantics(image: true, label: label, child: box);
    }
    return SimfTapToEnlarge(
      image: photo,
      label: label ?? '',
      child: box,
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

