import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The empty 56px bordered attach box: a centred label + trailing icon.
class AttachBox extends StatelessWidget {
  const AttachBox({
    required this.label,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: Container(
        height: 56,
        decoration: BoxDecoration(
          border: Border.all(color: SimfTokens.beigeBorder),
          borderRadius: SimfTokens.borderRadiusSmall,
        ),
        // The frame (168:2972) shows the gold attach glyph first, then the
        // label — forced LTR so the icon-then-text order holds under Arabic too
        // and the icon is gold, not grey (D-674).
        child: Directionality(
          textDirection: TextDirection.ltr,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Icon(icon, size: 24, color: SimfTokens.accent),
              const SizedBox(width: SimfTokens.space2),
              // BUG-019 — the box has a fixed height and sits in a half-width
              // tablet column, so a longer attach label must ellipsize instead
              // of overflowing. Loose fit: an already-fitting label is unmoved.
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: SimfTokens.inputInk,
                    fontSize: SimfTokens.textMd,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
