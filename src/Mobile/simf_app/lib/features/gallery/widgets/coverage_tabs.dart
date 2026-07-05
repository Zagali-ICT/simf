import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';

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
          child: _CoverageTab(
            label: l10n.galleryTitle,
            active: true,
            onTap: null,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _CoverageTab(
            label: l10n.mediaPartnersTitle,
            active: false,
            onTap: () => context.goNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _CoverageTab(
            label: l10n.newsTitle,
            active: false,
            onTap: () => context.goNamed(RouteNames.news),
          ),
        ),
      ],
    );
  }
}

/// One tab pill (frame node 947:3872): a 48-high card, solid gold when active
/// else a bordered navy card. Two-word labels wrap to two centred lines.
class _CoverageTab extends StatelessWidget {
  const _CoverageTab({
    required this.label,
    required this.active,
    required this.onTap,
  });

  final String label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      color: active ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: active ? SimfTokens.accent : SimfTokens.beigeBorder,
      child: SizedBox(
        height: 48,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                // Figma 947:3764 — the active gold pill carries dark navy text;
                // inactive pills carry beige text on navy.
                color: active ? SimfTokens.navy : SimfTokens.beigeBorder,
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
