import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/media_tab.dart';

/// Which media-coverage tab is active on the current screen.
enum MediaCoverageTab { partners, latestUpdates }

/// The two media-center tabs (Figma 1049:12629): الشركاء الإعلاميون · احدث
/// المستجدات. Shared by the News (#29) and Media-partners (#31) screens — the
/// active tab is a solid gold pill with white text; the inactive one is a
/// transparent pill with a beige hairline that navigates (replace) to its
/// route. (Figma 947/1049 dropped the معرض الصور tab from this strip — the
/// Gallery screen #30 has its own three-tab strip.)
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
          child: MediaTab(
            label: l10n.mediaPartnersTitle,
            active: partnersActive,
            onTap: partnersActive
                ? null
                : () => context.pushReplacementNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: MediaTab(
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
