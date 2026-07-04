import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The moderator desk navy header (Figma 1461:12565): back button (left),
/// centred RTL title, and the gold role pill (right). Forced LTR so the back
/// button sits on the left as drawn.
class ModeratorDeskHeader extends StatelessWidget {
  const ModeratorDeskHeader({
    required this.title,
    required this.badgeLabel,
    required this.onBack,
    super.key,
  });

  final String title;
  final String badgeLabel;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: SimfTokens.navyHeader,
      padding: const EdgeInsets.fromLTRB(8, 4, 16, 16),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          // Circular navy back button with a left chevron (Figma).
          Padding(
            padding: const EdgeInsets.all(8),
            child: Material(
              color: SimfTokens.navyDeep,
              shape: const CircleBorder(),
              child: InkWell(
                customBorder: const CircleBorder(),
                onTap: onBack,
                child: const Padding(
                  padding: EdgeInsets.all(6),
                  child: Icon(
                    Icons.chevron_left,
                    color: Colors.white,
                    size: 26,
                  ),
                ),
              ),
            ),
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              textDirection: TextDirection.rtl,
              style: const TextStyle(
                fontSize: 28,
                fontWeight: FontWeight.w700,
                color: Colors.white,
              ),
            ),
          ),
          _RolePill(label: badgeLabel),
        ],
      ),
    );
  }
}

class _RolePill extends StatelessWidget {
  const _RolePill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textTitle,
        ),
      ),
    );
  }
}
