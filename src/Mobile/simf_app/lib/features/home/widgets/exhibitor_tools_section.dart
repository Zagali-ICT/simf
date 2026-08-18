import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// Exhibitor (العارض) lead-capture tools — D-519. Shown only to the Exhibitor
/// role, above the shared attendee content, and owns its trailing gap.
class ExhibitorToolsSection extends StatelessWidget {
  const ExhibitorToolsSection({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfSectionHeader(title: l10n.exhibitorToolsSection),
        const SizedBox(height: SimfTokens.space4),
        SimfTileRow(
          children: <Widget>[
            SimfNavTile(
              label: l10n.scanVisitorTitle,
              icon: Icons.qr_code_scanner,
              minHeight: SimfTokens.navTileHeight,
              onTap: () => context.pushNamed(RouteNames.scanVisitor),
            ),
            SimfNavTile(
              label: l10n.myVisitorsTitle,
              icon: Icons.groups_outlined,
              minHeight: SimfTokens.navTileHeight,
              onTap: () => context.pushNamed(RouteNames.myVisitors),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }
}
