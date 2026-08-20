import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

class HubRow extends StatelessWidget {
  const HubRow({
    required this.title,
    required this.subtitle,
    required this.onTap,
    super.key,
  });

  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: SimfTokens.labelWhiteSemiboldLg,
                  ),
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    subtitle,
                    style: SimfTokens.labelBeigeSm,
                  ),
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // The thin stroked forward chevron the sibling cards use
            // (session-detail seat card / speakers rows). The old
            // direction-aware Icons.chevron_left DOUBLE-mirrored under RTL
            // (chevron_left carries matchTextDirection, so Flutter flipped it
            // back to pointing right) — the shared chevron carries the asset
            // as drawn and mirrors it in LTR only (D-601).
            const SimfForwardChevron(
              AppAssets.icBack,
              size: SimfTokens.hubRowSize,
              color: SimfTokens.beigeBorder,
            ),
          ],
        ),
      ),
    );
  }
}
