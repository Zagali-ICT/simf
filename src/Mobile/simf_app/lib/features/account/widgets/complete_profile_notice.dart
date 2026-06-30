import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// D-434 — a clear notice that this is the complete-profile step, so the user
/// pays attention to the required items (white-on-beige + gold border to stand
/// out from the card).
class CompleteProfileNotice extends StatelessWidget {
  const CompleteProfileNotice({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: SimfTokens.surface,
        border: Border.all(color: SimfTokens.accent),
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
      child: Row(
        children: <Widget>[
          const Icon(
            Icons.info_outline,
            size: 18,
            color: SimfTokens.accent,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              l10n.completeProfilePrompt,
              style: const TextStyle(
                fontSize: 12,
                color: SimfTokens.inputInk,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
