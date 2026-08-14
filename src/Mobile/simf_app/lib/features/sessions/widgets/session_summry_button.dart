import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold ملخص الجلسة button (Figma 1388:7621) — opens the session summary
/// (34). When [enabled] is false (no published summary yet) it greys out with
/// the shell's disabled tokens and stops tapping — inactive, not hidden (owner
/// 2026-07-14, same treatment as the detail header's ملخص الجلسة button).
class SessionSummryButton extends StatelessWidget {
  const SessionSummryButton({
    required this.label,
    required this.enabled,
    required this.onTap,
    super.key,
  });

  final String label;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final fg = enabled ? SimfTokens.surface : SimfTokens.navyDisabledText;
    final button = Material(
      color: enabled ? SimfTokens.accent : SimfTokens.navyDisabled,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: enabled ? onTap : null,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2), // p-8
          child: Text(
            label,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
    // Disabled: a deeper no-op tap recogniser wins the gesture arena over the
    // card's open-detail InkWell, so tapping an inactive button does nothing
    // (owner 2026-07-14) rather than falling through to the card.
    return enabled
        ? button
        : GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () {},
            child: button,
          );
  }
}
