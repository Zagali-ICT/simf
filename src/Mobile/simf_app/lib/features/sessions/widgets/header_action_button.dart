import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// One header-card action button (frame 889:2708/889:2709): a 34-high navy chip
/// on the 4px radius with a centred 12px SemiBold label. The accented variant
/// (ملخص الجلسة) carries the 0.5px gold hairline + gold text; the plain variant
/// (رابط الجلسة) the 0.2px beige hairline + white text.
class HeaderActionButton extends StatelessWidget {
  const HeaderActionButton({required this.label, required this.accented, required this.enabled, required this.onTap, super.key,
  });

  final String label;
  final bool accented;

  /// False greys the chip and drops the tap (owner 2026-07-14): an inactive
  /// action whose state does not apply yet (a summary on a future session).
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Disabled overrides the accent/plain palette with the shell's disabled
    // tokens (same greying the locked اسأل المحاور / بطاقتي cards use).
    final fg = !enabled
        ? SimfTokens.navyDisabledText
        : (accented ? SimfTokens.accent : SimfTokens.surface);
    final borderColor = !enabled
        ? SimfTokens.navyDisabledBorder
        : (accented ? SimfTokens.accent : SimfTokens.beigeBorder);
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          height: SimfTokens.actionChipHeight,
          alignment: Alignment.center,
          padding: const EdgeInsets.all(SimfTokens.space2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            border: Border.all(
              color: borderColor,
              width: accented ? SimfTokens.hairlineBold : SimfTokens.hairline,
            ),
          ),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
  }
}
