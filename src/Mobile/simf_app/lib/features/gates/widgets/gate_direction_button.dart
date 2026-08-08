import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One دخول/خروج movement toggle pill (setup stage).
class GateDirectionButton extends StatelessWidget {
  const GateDirectionButton({
    required this.label,
    required this.icon,
    required this.selected,
    required this.enabled,
    required this.onTap,
    super.key,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Figma 758:4687/4693 — selected = gold fill, unselected = navy; both with a
    // thin beige hairline and white bold label/icon. Radius 4.
    return Opacity(
      opacity: enabled ? 1 : SimfTokens.opacityDisabled,
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          height: SimfTokens.controlHeight,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? SimfTokens.accent : SimfTokens.navy,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            border: Border.all(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairlineBold,
            ),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Text(
                label,
                style: SimfTokens.labelWhiteBoldLg,
              ),
              const SizedBox(width: SimfTokens.space2),
              Icon(icon, size: SimfTokens.gateDirectionButtonSize, color: SimfTokens.surface),
            ],
          ),
        ),
      ),
    );
  }
}
