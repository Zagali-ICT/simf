import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class SocialButton extends StatelessWidget {
  const SocialButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          width: 48,
          height: 48,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.navy,
            borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
            border: Border.all(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairline,
            ),
          ),
          child: Icon(icon, color: SimfTokens.surface, size: 20),
        ),
      ),
    );
  }
}
