import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

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
      opacity: enabled ? 1 : 0.5,
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          height: 48,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? SimfTokens.accent : SimfTokens.navy,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            border: Border.all(color: SimfTokens.beigeBorder, width: 0.5),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Text(
                label,
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textLg,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Icon(icon, size: 18, color: Colors.white),
            ],
          ),
        ),
      ),
    );
  }
}
