import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The full-width gold action button: radius-4 gold fill with the centred white
/// label. Stays gold while [loading] (taps disabled, a white spinner replaces the
/// label) so the button never turns into an unreadable dark box on the navy.
class RateGoldButton extends StatelessWidget {
  const RateGoldButton({
    required this.label,
    required this.onTap,
    this.loading = false,
    super.key,
  });

  final String label;
  final bool loading;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.accent,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: loading ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: SizedBox(
          height: SimfTokens.controlHeight,
          child: Center(
            child: loading
                ? const SizedBox(
                    width: SimfTokens.space5,
                    height: SimfTokens.space5,
                    child: CircularProgressIndicator(
                      strokeWidth: SimfTokens.rateGoldButtonStrokeWidth,
                      color: SimfTokens.surface,
                    ),
                  )
                : Text(
                    label,
                    style: SimfTokens.labelWhiteMediumLg,
                  ),
          ),
        ),
      ),
    );
  }
}
