import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// One tab pill (frame node 947:3872): a 48-high card, solid gold when active
/// else a bordered navy card. Two-word labels wrap to two centred lines.
class CoverageTab extends StatelessWidget {
  const CoverageTab({required this.label, required this.active, required this.onTap, super.key,
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
        height: SimfTokens.controlHeight,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              // Figma 947:3764 — the active gold pill carries dark navy text;
              // inactive pills carry beige text on navy.
              style: active
                  ? SimfTokens.labelNavySemiboldSm
                  : SimfTokens.labelBeigeSemiboldSm,
            ),
          ),
        ),
      ),
    );
  }
}
