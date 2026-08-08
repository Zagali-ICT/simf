import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import '../theme/app_assets.dart';
import '../theme/tokens.dart';

/// The raised gold QR centre action (frame 758:1476 "boxicons:qr", 56px) — the
/// bundled multi-colour SVG (gold disc, cream ring, navy glyph) rendered as-is.
class CentreAction extends StatelessWidget {
  const CentreAction({
    required this.active,
    required this.label,
    required this.onTap,
  });

  final bool active;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Center(
        child: Semantics(
          button: true,
          selected: active,
          label: label,
          child: InkWell(
            onTap: active ? null : onTap,
            customBorder: const CircleBorder(),
            child: SvgPicture.asset(
              AppAssets.navQr,
              width: SimfTokens.centreActionWidth,
              height: SimfTokens.centreActionHeight,
            ),
          ),
        ),
      ),
    );
  }
}
