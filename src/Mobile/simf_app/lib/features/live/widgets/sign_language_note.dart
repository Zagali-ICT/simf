import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// A sign-language-available note — a gold icon + muted label (shown when a
/// sign feed is announced with no main feed to toggle into).
class SignLanguageNote extends StatelessWidget {
  const SignLanguageNote({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        const Icon(
          Icons.sign_language_outlined,
          size: SimfTokens.liveContentSizeSm,
          color: SimfTokens.accent,
        ),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            label,
            style: SimfTokens.labelBeigeSm,
          ),
        ),
      ],
    );
  }
}

// A20 (2026-07-26) — `RegionNoticeCard` (frame 934:3619, the gold "available
// only inside the Riyadh region per regulations" card) is deleted with its only
// caller: nothing in the app, API or CP ever checked the viewer's location, so
// the notice was an unconditional false claim. Geo-fencing the stream for real
// is a product/legal decision, not a defect fix.
//
// FR-702 (owner 2026-07-31) — that product decision was taken, and it is "no
// restriction, this is only notification": [LiveNoticeBanner] below replaces
// the hard-coded region claim with the free-text notice the admin authors per
// session in the Control Panel.
