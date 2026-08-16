import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_app_shell.dart'
    show SimfShellScope, tabIndex;
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart' show SimfPageShell;
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

// Back and menu controls, and the standard back action.
// Split out of `simf_page_shell.dart` (one widget group per file,
// CLAUDE.md section 1). That file re-exports this one, so every existing
// import of the shell keeps resolving.

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

/// The circled back chevron (dark circle, white chevron). By default the glyph
/// always points left — the D-363 forced-LTR header pattern still used by the
/// speakers / session-detail / speaker-profile headers. In the shared
/// natural-direction [SimfPageShell] header [mirrorInRtl] is set, so the
/// chevron mirrors to point right under RTL, where the leading control sits at
/// the inline start (physical right).
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
          size: SimfTokens.simfPageShellSizeMd,
          color: SimfTokens.surface,
        ),
      ),
    );
  }
}

/// The circled drawer ☰ control (same dark circle as [SimfCircledBackButton]) —
/// opens the shell's side menu. Lives in the shared header's trailing
/// controller.
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
      icon: const Icon(Icons.menu,
          color: SimfTokens.surface, size: SimfTokens.simfPageShellSizeSm,),
    );
  }
}
