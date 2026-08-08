import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// The red LIVE banner (frame node 210:736) — static config for now (no API,
/// D10, Page_013 L-6); tapping it opens the live view.
class LiveBanner extends StatelessWidget {
  const LiveBanner({required this.l10n, required this.onTap, super.key});

  final AppL10n l10n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final flip = !l10n.isArabic;
    return Material(
      color: SimfTokens.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.danger,
          width: SimfTokens.hairlineBold,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Row(
            children: <Widget>[
              Container(
                width: SimfTokens.liveBadgeSize,
                height: SimfTokens.liveBadgeSize,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.danger,
                  borderRadius: BorderRadius.circular(SimfTokens.radius),
                ),
                child: Text(
                  l10n.liveNowLabel,
                  // Frame 758:1157 — the "مباشر" badge is SemiBold.
                  style: SimfTokens.labelWhiteSemibold,
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.homeLiveTitle,
                      style: SimfTokens.labelWhiteSemibold,
                    ),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      l10n.homeLiveSubtitle,
                      style: SimfTokens.bodyWhiteSm,
                    ),
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              // The LIVE (YouTube/broadcast) banner's caret matches the home
              // section rows: Figma 758:1151 fills it WHITE (like the عن الملتقى
              // / الرعاه / الأخبار carets — only روح السعودية 758:1275 is gold).
              // The bundled SVG points left and does not mirror under RTL.
              // Flip horizontally in English so the caret points right →
              // (forward in LTR reading direction).
              Transform.flip(
                flipX: flip,
                child: const SimfSvgIcon(
                  AppAssets.icCaretLeft,
                  color: SimfTokens.surface,
                  size: SimfTokens.homeBannersSize,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
