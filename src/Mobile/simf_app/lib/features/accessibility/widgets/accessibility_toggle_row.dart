import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One labelled switch row (frame 1116:16630): the navy-deep box with the title
/// at the inline start and the gold switch at the inline end. [hint] rides the
/// switch as a semantics hint (this is the accessibility screen, after all).
class AccessibilityToggleRow extends StatelessWidget {
  const AccessibilityToggleRow({
    required this.title,
    required this.hint,
    required this.value,
    required this.onChanged,
    super.key,
  });

  final String title;
  final String hint;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.start,
              style: SimfTokens.labelWhiteMedium,
            ),
          ),
          Semantics(
            hint: hint,
            child: Switch(
              value: value,
              onChanged: onChanged,
              activeThumbColor: Colors.white,
              activeTrackColor: SimfTokens.accent,
              inactiveThumbColor: Colors.white,
              inactiveTrackColor: SimfTokens.navy,
              trackOutlineColor:
                  WidgetStateProperty.all(SimfTokens.beigeBorder),
            ),
          ),
        ],
      ),
    );
  }
}
