import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import 'coverage_tab.dart';

/// The three media-coverage tabs (frame node 947:3869): the active tab is solid
/// gold, the others bordered navy cards. The gallery tab is active here; the
/// other two navigate to their routes (the app models each tab as its own
/// screen — frames 948:3961 / the partners frame).
class CoverageTabs extends StatelessWidget {
  const CoverageTabs({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // Figma 947:3764 (Arabic/RTL): the active معرض الصور والفيديوهات tab is the
    // right-most (inline-start), then الشركاء الإعلاميون, then الأخبار on the
    // left. A Row lays children start→end, so the order is gallery → partners →
    // news.
    return Row(
      children: <Widget>[
        Expanded(
          child: CoverageTab(
            label: l10n.galleryTitle,
            active: true,
            onTap: null,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: CoverageTab(
            label: l10n.mediaPartnersTitle,
            active: false,
            onTap: () => context.goNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: CoverageTab(
            label: l10n.newsTitle,
            active: false,
            onTap: () => context.goNamed(RouteNames.news),
          ),
        ),
      ],
    );
  }
}

