import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The اسأل المحاور card (frame 1056:12876): a full-width navy box with a beige
/// hairline holding a centred user glyph over the 12px SemiBold label. Opens
/// send-question (26) for this session.
class AskHostCard extends StatelessWidget {
  const AskHostCard({
    required this.label,
    required this.onTap,
    required this.enabled,
    this.disabledHint,
    super.key,
  });

  final String label;
  final VoidCallback onTap;

  /// #3 — true once the user has joined the session; false disables the tap and
  /// shows [disabledHint] (the pre-ask gate — join to ask, no check-in needed).
  final bool enabled;
  final String? disabledHint;

  @override
  Widget build(BuildContext context) {
    final color = enabled ? Colors.white : SimfTokens.navyDisabledText;
    // Frame 1056:12876 — the user glyph is gold (accent) over a white label.
    final iconColor =
        enabled ? SimfTokens.accent : SimfTokens.navyDisabledText;
    return SimfCard(
      onTap: enabled ? onTap : null,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            // Figma 1056:12877 — solar:user-outline, the design-system user
            // glyph (bundled), not Material's person_outline.
            SimfSvgIcon(
              'assets/icons/nav_user.svg',
              size: 24,
              color: iconColor,
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              label,
              style: TextStyle(
                color: color,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textSm,
              ),
            ),
            if (!enabled && disabledHint != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space1),
              Text(
                disabledHint!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textXs,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
