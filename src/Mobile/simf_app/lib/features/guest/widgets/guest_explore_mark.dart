import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The accent explore mark at the top of the guest-mode entry screen: the
/// compass glyph inside a gold-bordered, faintly gold-filled circle.
class GuestExploreMark extends StatelessWidget {
  const GuestExploreMark({super.key});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: SimfTokens.guestModeScreenWidthMd,
        height: SimfTokens.guestModeScreenHeightMd,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: SimfTokens.accent.withValues(alpha: 0.08),
          border: Border.all(
            color: SimfTokens.accent,
            width: SimfTokens.guestModeScreenWidthSm,
          ),
          shape: BoxShape.circle,
        ),
        child: const Icon(
          Icons.explore_outlined,
          size: SimfTokens.guestModeScreenSize,
          color: SimfTokens.accent,
        ),
      ),
    );
  }
}
