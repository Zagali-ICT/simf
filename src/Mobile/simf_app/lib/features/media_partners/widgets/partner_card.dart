import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_image_viewer.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/media_partners/widgets/partner_logo.dart';

/// One partner — frame node 958:2263: the navy KSA card with a centred gold
/// rounded-square logo holder over the partner name (white 12px SemiBold).
///
/// FR-LGO-003 — the WHOLE card is the tap target, not just the 48px logo box:
/// pressing anywhere on it opens the partner's mark full size in the shared
/// [SimfImageViewer], the same press-to-enlarge affordance the sponsor and
/// exhibitor surfaces carry. Media partners have no detail route (the frame
/// defines none), so the card used to be completely inert.
class PartnerCard extends StatelessWidget {
  const PartnerCard({required this.name, required this.logoUrl, super.key});

  final String name;
  final String logoUrl;

  @override
  Widget build(BuildContext context) {
    final url = logoUrl.trim();
    final Widget body = SimfCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            PartnerLogo(url: url, name: name),
            const SizedBox(height: SimfTokens.space2),
            // Flexible so a long Arabic name (or a large OS text-scale) shrinks
            // + ellipsises inside the fixed-aspect grid cell instead of
            // overflowing the column.
            Flexible(
              child: Text(
                name,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: SimfTokens.labelWhiteSemiboldSm13,
              ),
            ),
          ],
        ),
      ),
    );
    // A partner with no logo has nothing to enlarge — it keeps the plain card
    // rather than advertising a press that would open an initials tile.
    if (url.isEmpty) {
      return body;
    }
    return SimfTapToEnlarge(
      image: NetworkImage(url),
      label: name,
      child: body,
    );
  }
}
