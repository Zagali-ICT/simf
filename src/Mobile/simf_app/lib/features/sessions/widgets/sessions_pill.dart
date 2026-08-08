import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class SessionsPill extends StatelessWidget {
  const SessionsPill({
    required this.label,
    required this.selected,
    required this.onTap,
    this.icon,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final Color fg = selected ? SimfTokens.surface : SimfTokens.beigeBorder;
    final Widget text = Text(
      label,
      textAlign: TextAlign.center,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: fg,
        fontSize: SimfTokens.textSm, // 12
        fontWeight: FontWeight.w600,
      ),
    );
    return Material(
      color: selected ? SimfTokens.accent : SimfTokens.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: selected
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2), // p-8
          child: icon == null
              ? text
              : Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Flexible(child: text),
                    const SizedBox(width: SimfTokens.space1), // gap-4
                    Icon(icon, size: SimfTokens.sessionsPillSize, color: fg),
                  ],
                ),
        ),
      ),
    );
  }
}
