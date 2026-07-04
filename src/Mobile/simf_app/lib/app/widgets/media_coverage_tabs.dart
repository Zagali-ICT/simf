import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../localization/app_l10n.dart';
import '../route_names.dart';
import '../theme/tokens.dart';

/// Which media-coverage tab is active on the current screen.
enum MediaCoverageTab { partners, latestUpdates }

/// The two media-center tabs (Figma 1049:12629): الشركاء الإعلاميون · احدث
/// المستجدات. Shared by the News (#29) and Media-partners (#31) screens — the
/// active tab is a solid gold pill with white text; the inactive one is a
/// transparent pill with a beige hairline that navigates (replace) to its route.
/// (Figma 947/1049 dropped the معرض الصور tab from this strip — the Gallery
/// screen #30 has its own three-tab strip.)
class MediaCoverageTabs extends StatelessWidget {
  const MediaCoverageTabs({required this.active, super.key});

  final MediaCoverageTab active;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final partnersActive = active == MediaCoverageTab.partners;
    final updatesActive = active == MediaCoverageTab.latestUpdates;
    // Figma 1049:12629 (Arabic/RTL): right→left الشركاء الإعلاميون · احدث
    // المستجدات. A Row lays children start→end, so the first child is the
    // right-most: partners then latest-updates.
    return Row(
      children: <Widget>[
        Expanded(
          child: _MediaTab(
            label: l10n.mediaPartnersTitle,
            active: partnersActive,
            onTap: partnersActive
                ? null
                : () => context.pushReplacementNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _MediaTab(
            label: l10n.latestUpdatesTitle,
            active: updatesActive,
            onTap: updatesActive
                ? null
                : () => context.pushReplacementNamed(RouteNames.news),
          ),
        ),
      ],
    );
  }
}

/// One media-center tab pill (frame node 1049:12640 / 1049:12642): a 48-high,
/// 8px-radius pill — active is solid gold with **white** text; inactive is a
/// transparent pill with a beige 0.2 hairline and beige text.
class _MediaTab extends StatelessWidget {
  const _MediaTab({required this.label, required this.active, this.onTap});

  final String label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? SimfTokens.accent : Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: active
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          height: 48,
          child: Center(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                  // Figma 1049 — the active gold pill carries white text.
                  color: active ? Colors.white : SimfTokens.beigeBorder,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
